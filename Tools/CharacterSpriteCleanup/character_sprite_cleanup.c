#include <MagickWand/MagickWand.h>

#include <ctype.h>
#include <errno.h>
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/types.h>

typedef struct {
    uint8_t r;
    uint8_t g;
    uint8_t b;
} Rgb;

typedef enum {
    HOLE_CUT_ALPHA,
    HOLE_CUT_ALPHA_ALL,
    HOLE_INPAINT_DARK
} HoleMode;

typedef struct {
    char *path;
    size_t expected_width;
    size_t expected_height;
    size_t x0;
    size_t y0;
    size_t x1;
    size_t y1;
    HoleMode mode;
} HoleRule;

typedef struct {
    HoleRule *items;
    size_t count;
} HoleRules;

static int compare_int(const void *left, const void *right)
{
    const int a = *(const int *)left;
    const int b = *(const int *)right;
    return (a > b) - (a < b);
}

static int compare_byte(const void *left, const void *right)
{
    const uint8_t a = *(const uint8_t *)left;
    const uint8_t b = *(const uint8_t *)right;
    return (a > b) - (a < b);
}

static int mkdir_parents(const char *path)
{
    char *copy = strdup(path);
    char *cursor;

    if (copy == NULL) {
        return 0;
    }

    for (cursor = copy + 1; *cursor != '\0'; ++cursor) {
        if (*cursor != '/') {
            continue;
        }
        *cursor = '\0';
        if (mkdir(copy, 0775) != 0 && errno != EEXIST) {
            free(copy);
            return 0;
        }
        *cursor = '/';
    }

    free(copy);
    return 1;
}

static uint16_t *distance_from_transparency(
    const uint8_t *rgba,
    size_t width,
    size_t height)
{
    const size_t count = width * height;
    uint16_t *distance = malloc(count * sizeof(*distance));
    size_t *queue = malloc(count * sizeof(*queue));
    size_t head = 0;
    size_t tail = 0;
    size_t index;

    if (distance == NULL || queue == NULL) {
        free(distance);
        free(queue);
        return NULL;
    }

    for (index = 0; index < count; ++index) {
        if (rgba[index * 4 + 3] <= 8) {
            distance[index] = 0;
            queue[tail++] = index;
        } else {
            distance[index] = UINT16_MAX;
        }
    }

    while (head < tail) {
        const size_t current = queue[head++];
        const size_t x = current % width;
        const size_t y = current / width;
        const uint16_t next_distance = (uint16_t)(distance[current] + 1);
        int dy;
        int dx;

        if (next_distance > 32) {
            continue;
        }

        for (dy = -1; dy <= 1; ++dy) {
            for (dx = -1; dx <= 1; ++dx) {
                size_t neighbor;
                long nx;
                long ny;

                if (dx == 0 && dy == 0) {
                    continue;
                }
                nx = (long)x + dx;
                ny = (long)y + dy;
                if (nx < 0 || ny < 0 || nx >= (long)width || ny >= (long)height) {
                    continue;
                }
                neighbor = (size_t)ny * width + (size_t)nx;
                if (distance[neighbor] != UINT16_MAX) {
                    continue;
                }
                distance[neighbor] = next_distance;
                queue[tail++] = neighbor;
            }
        }
    }

    free(queue);
    return distance;
}

static int local_inner_reference(
    const uint8_t *rgba,
    const uint16_t *distance,
    size_t width,
    size_t height,
    size_t x,
    size_t y,
    Rgb *reference)
{
    const uint16_t origin_distance = distance[y * width + x];
    int radius;

    for (radius = 1; radius <= 5; ++radius) {
        Rgb samples[121];
        int luminance[121];
        int sorted_luminance[121];
        uint8_t red[121];
        uint8_t green[121];
        uint8_t blue[121];
        size_t sample_count = 0;
        long yy;
        long xx;
        const long y0 = (long)y - radius < 0 ? 0 : (long)y - radius;
        const long x0 = (long)x - radius < 0 ? 0 : (long)x - radius;
        const long y1 = (long)y + radius >= (long)height
            ? (long)height - 1
            : (long)y + radius;
        const long x1 = (long)x + radius >= (long)width
            ? (long)width - 1
            : (long)x + radius;
        int cutoff;
        size_t kept = 0;
        size_t index;

        for (yy = y0; yy <= y1; ++yy) {
            for (xx = x0; xx <= x1; ++xx) {
                const size_t pixel = (size_t)yy * width + (size_t)xx;
                const uint8_t *source = rgba + pixel * 4;

                if (source[3] < 240 || distance[pixel] < origin_distance + 1) {
                    continue;
                }
                samples[sample_count].r = source[0];
                samples[sample_count].g = source[1];
                samples[sample_count].b = source[2];
                luminance[sample_count] =
                    ((int)source[0] + (int)source[1] + (int)source[2]) / 3;
                sorted_luminance[sample_count] = luminance[sample_count];
                ++sample_count;
            }
        }

        if (sample_count == 0) {
            continue;
        }

        qsort(sorted_luminance, sample_count, sizeof(*sorted_luminance), compare_int);
        cutoff = sorted_luminance[(sample_count - 1) * 40 / 100];
        for (index = 0; index < sample_count; ++index) {
            if (luminance[index] > cutoff) {
                continue;
            }
            red[kept] = samples[index].r;
            green[kept] = samples[index].g;
            blue[kept] = samples[index].b;
            ++kept;
        }

        if (kept == 0) {
            continue;
        }

        qsort(red, kept, sizeof(*red), compare_byte);
        qsort(green, kept, sizeof(*green), compare_byte);
        qsort(blue, kept, sizeof(*blue), compare_byte);
        reference->r = red[kept / 2];
        reference->g = green[kept / 2];
        reference->b = blue[kept / 2];
        return 1;
    }

    return 0;
}

static size_t clean_boundary(
    const uint8_t *source,
    uint8_t *output,
    size_t width,
    size_t height)
{
    const size_t count = width * height;
    uint16_t *distance = distance_from_transparency(source, width, height);
    size_t changed = 0;
    size_t index;

    if (distance == NULL) {
        return SIZE_MAX;
    }

    memcpy(output, source, count * 4);
    for (index = 0; index < count; ++index) {
        const uint8_t *pixel = source + index * 4;
        uint8_t *target = output + index * 4;
        const uint8_t alpha = pixel[3];
        const uint8_t minimum = pixel[0] < pixel[1]
            ? (pixel[0] < pixel[2] ? pixel[0] : pixel[2])
            : (pixel[1] < pixel[2] ? pixel[1] : pixel[2]);
        const uint8_t maximum = pixel[0] > pixel[1]
            ? (pixel[0] > pixel[2] ? pixel[0] : pixel[2])
            : (pixel[1] > pixel[2] ? pixel[1] : pixel[2]);
        const double source_luma = ((double)pixel[0] + pixel[1] + pixel[2]) / 3.0;
        Rgb reference;
        double reference_luma;
        double excess;
        int suspicious;

        if (alpha <= 8 || distance[index] < 1 || distance[index] > 2) {
            continue;
        }
        if (!local_inner_reference(
                source,
                distance,
                width,
                height,
                index % width,
                index / width,
                &reference)) {
            continue;
        }

        reference_luma = ((double)reference.r + reference.g + reference.b) / 3.0;
        excess = source_luma - reference_luma;

        /*
         * The source sprites were cut from artwork composited over white.  Their
         * antialiased edge RGB therefore still contains that white matte even
         * though the alpha channel was later restored.  Reverse the white
         * composite for translucent boundary pixels.  This keeps alpha and the
         * silhouette unchanged, and naturally leaves a genuinely white shirt or
         * cuff white.
         */
        if (alpha < 240) {
            int channel;

            for (channel = 0; channel < 3; ++channel) {
                const int unmatted = 255
                    - ((255 - (int)pixel[channel]) * 255 + alpha / 2) / alpha;
                target[channel] = (uint8_t)(unmatted < 0 ? 0 : unmatted);
            }
            if (memcmp(pixel, target, 3) != 0) {
                ++changed;
            }
            continue;
        }

        /*
         * A small number of matte flecks are fully opaque.  Recolour only
         * neutral, boundary-adjacent pixels that are materially brighter than
         * their inward neighbours.  Intentional white details remain protected
         * because their inward reference is also light.
         */
        suspicious = excess >= 24.0
            && source_luma >= 105.0
            && (int)maximum - minimum <= 52;
        if (!suspicious) {
            continue;
        }
        target[0] = reference.r;
        target[1] = reference.g;
        target[2] = reference.b;
        ++changed;
    }

    free(distance);
    return changed;
}

static int pixel_is_seed(const uint8_t *pixel, HoleMode mode)
{
    const uint8_t minimum = pixel[0] < pixel[1]
        ? (pixel[0] < pixel[2] ? pixel[0] : pixel[2])
        : (pixel[1] < pixel[2] ? pixel[1] : pixel[2]);
    const uint8_t maximum = pixel[0] > pixel[1]
        ? (pixel[0] > pixel[2] ? pixel[0] : pixel[2])
        : (pixel[1] > pixel[2] ? pixel[1] : pixel[2]);

    if (mode == HOLE_INPAINT_DARK) {
        return pixel[3] >= 240 && minimum >= 225 && (int)maximum - minimum <= 22;
    }
    if (mode == HOLE_CUT_ALPHA_ALL) {
        const int luma = ((int)pixel[0] + pixel[1] + pixel[2]) / 3;
        return pixel[3] > 16 && luma >= 45 && (int)maximum - minimum < 60;
    }
    return pixel[3] > 51 && minimum > 140 && (int)maximum - minimum < 31;
}

static int pixel_is_eligible(const uint8_t *pixel, HoleMode mode)
{
    const uint8_t minimum = pixel[0] < pixel[1]
        ? (pixel[0] < pixel[2] ? pixel[0] : pixel[2])
        : (pixel[1] < pixel[2] ? pixel[1] : pixel[2]);
    const uint8_t maximum = pixel[0] > pixel[1]
        ? (pixel[0] > pixel[2] ? pixel[0] : pixel[2])
        : (pixel[1] > pixel[2] ? pixel[1] : pixel[2]);
    const int chroma = (int)maximum - minimum;
    const int luma = ((int)pixel[0] + pixel[1] + pixel[2]) / 3;

    if (mode == HOLE_INPAINT_DARK) {
        return pixel[3] >= 240 && chroma <= 22 && luma >= 95;
    }
    if (mode == HOLE_CUT_ALPHA_ALL) {
        return pixel[3] > 16 && luma >= 45 && chroma < 60;
    }
    return pixel[3] > 51 && minimum > 102 && chroma < 46;
}

static int median_dark_reference(
    const uint8_t *rgba,
    const uint8_t *selected,
    size_t width,
    size_t height,
    size_t x,
    size_t y,
    Rgb *reference)
{
    int radius;

    for (radius = 1; radius <= 4; ++radius) {
        uint8_t red[81];
        uint8_t green[81];
        uint8_t blue[81];
        size_t count = 0;
        long yy;
        long xx;
        const long y0 = (long)y - radius < 0 ? 0 : (long)y - radius;
        const long x0 = (long)x - radius < 0 ? 0 : (long)x - radius;
        const long y1 = (long)y + radius >= (long)height
            ? (long)height - 1
            : (long)y + radius;
        const long x1 = (long)x + radius >= (long)width
            ? (long)width - 1
            : (long)x + radius;

        for (yy = y0; yy <= y1; ++yy) {
            for (xx = x0; xx <= x1; ++xx) {
                const size_t index = (size_t)yy * width + (size_t)xx;
                const uint8_t *pixel = rgba + index * 4;
                const int luma = ((int)pixel[0] + pixel[1] + pixel[2]) / 3;

                if (selected[index] || pixel[3] < 240 || luma >= 82) {
                    continue;
                }
                red[count] = pixel[0];
                green[count] = pixel[1];
                blue[count] = pixel[2];
                ++count;
            }
        }

        if (count == 0) {
            continue;
        }
        qsort(red, count, sizeof(*red), compare_byte);
        qsort(green, count, sizeof(*green), compare_byte);
        qsort(blue, count, sizeof(*blue), compare_byte);
        reference->r = red[count / 2];
        reference->g = green[count / 2];
        reference->b = blue[count / 2];
        return 1;
    }

    return 0;
}

static int apply_hole_rule(
    uint8_t *rgba,
    size_t width,
    size_t height,
    const HoleRule *rule,
    size_t *changed_alpha,
    size_t *changed_rgb)
{
    const size_t count = width * height;
    uint8_t *eligible = calloc(count, 1);
    uint8_t *selected = calloc(count, 1);
    size_t *queue = malloc(count * sizeof(*queue));
    size_t head = 0;
    size_t tail = 0;
    size_t y;
    size_t x;
    int result = 0;

    if (eligible == NULL || selected == NULL || queue == NULL) {
        fprintf(stderr, "Out of memory while applying reviewed mask to %s\n", rule->path);
        goto cleanup;
    }
    if (width != rule->expected_width || height != rule->expected_height
        || rule->x0 >= rule->x1 || rule->y0 >= rule->y1
        || rule->x1 > width || rule->y1 > height) {
        fprintf(stderr, "Reviewed mask geometry mismatch for %s\n", rule->path);
        goto cleanup;
    }

    for (y = rule->y0; y < rule->y1; ++y) {
        for (x = rule->x0; x < rule->x1; ++x) {
            const size_t index = y * width + x;
            const uint8_t *pixel = rgba + index * 4;
            int seed = 0;

            eligible[index] = (uint8_t)pixel_is_eligible(pixel, rule->mode);
            if (rule->mode == HOLE_CUT_ALPHA_ALL && eligible[index]) {
                int dy;
                int dx;
                const int luma = ((int)pixel[0] + pixel[1] + pixel[2]) / 3;

                /* A bright neutral island inside a reviewed gap may be fully
                 * enclosed by darker matte remnants.  It is still a valid
                 * starting point; the flood then follows only neutral pixels. */
                seed = luma >= 105;

                for (dy = -1; dy <= 1 && !seed; ++dy) {
                    for (dx = -1; dx <= 1; ++dx) {
                        const long nx = (long)x + dx;
                        const long ny = (long)y + dy;

                        if ((dx == 0 && dy == 0)
                            || nx < 0 || ny < 0
                            || nx >= (long)width || ny >= (long)height) {
                            continue;
                        }
                        if (rgba[((size_t)ny * width + (size_t)nx) * 4 + 3] <= 8) {
                            seed = 1;
                            break;
                        }
                    }
                }
            } else {
                seed = pixel_is_seed(pixel, rule->mode);
            }
            if (!seed) {
                continue;
            }
            selected[index] = 1;
            queue[tail++] = index;
        }
    }
    if (tail == 0) {
        fprintf(stderr, "Reviewed mask found no white seed in %s ROI %zu,%zu,%zu,%zu\n",
            rule->path, rule->x0, rule->y0, rule->x1, rule->y1);
        goto cleanup;
    }

    while (head < tail) {
        const size_t current = queue[head++];
        const size_t cx = current % width;
        const size_t cy = current / width;
        int dy;
        int dx;

        for (dy = -1; dy <= 1; ++dy) {
            for (dx = -1; dx <= 1; ++dx) {
                long nx;
                long ny;
                size_t neighbor;

                if (dx == 0 && dy == 0) {
                    continue;
                }
                nx = (long)cx + dx;
                ny = (long)cy + dy;
                if (nx < (long)rule->x0 || nx >= (long)rule->x1
                    || ny < (long)rule->y0 || ny >= (long)rule->y1) {
                    continue;
                }
                neighbor = (size_t)ny * width + (size_t)nx;
                if (!eligible[neighbor] || selected[neighbor]) {
                    continue;
                }
                selected[neighbor] = 1;
                queue[tail++] = neighbor;
            }
        }
    }

    if (tail > 350) {
        fprintf(stderr, "Reviewed mask flood too large for %s: %zu pixels\n", rule->path, tail);
        goto cleanup;
    }

    if (rule->mode == HOLE_CUT_ALPHA || rule->mode == HOLE_CUT_ALPHA_ALL) {
        for (head = 0; head < tail; ++head) {
            uint8_t *pixel = rgba + queue[head] * 4;
            if (pixel[3] != 0) {
                pixel[3] = 0;
                ++*changed_alpha;
            }
        }
    } else {
        for (head = 0; head < tail; ++head) {
            const size_t index = queue[head];
            uint8_t *pixel = rgba + index * 4;
            Rgb reference;

            if (!median_dark_reference(
                    rgba,
                    selected,
                    width,
                    height,
                    index % width,
                    index / width,
                    &reference)) {
                fprintf(stderr, "No dark inpaint reference for %s pixel %zu,%zu\n",
                    rule->path, index % width, index / width);
                goto cleanup;
            }
            pixel[0] = reference.r;
            pixel[1] = reference.g;
            pixel[2] = reference.b;
            ++*changed_rgb;
        }
    }

    result = 1;

cleanup:
    free(eligible);
    free(selected);
    free(queue);
    return result;
}

static void destroy_hole_rules(HoleRules *rules)
{
    size_t index;
    for (index = 0; index < rules->count; ++index) {
        free(rules->items[index].path);
    }
    free(rules->items);
    rules->items = NULL;
    rules->count = 0;
}

static int load_hole_rules(const char *path, HoleRules *rules)
{
    FILE *file = fopen(path, "r");
    char line[8192];

    if (file == NULL) {
        fprintf(stderr, "Cannot open reviewed-mask manifest %s: %s\n", path, strerror(errno));
        return 0;
    }

    while (fgets(line, sizeof(line), file) != NULL) {
        char *fields[9];
        char *cursor = line;
        size_t field_count = 0;
        HoleRule rule;
        HoleRule *expanded;

        while (field_count < 9) {
            fields[field_count++] = cursor;
            cursor = strchr(cursor, ',');
            if (cursor == NULL) {
                break;
            }
            *cursor++ = '\0';
        }
        if (field_count == 0 || fields[0][0] == '#' || fields[0][0] == '\n') {
            continue;
        }
        if (field_count != 8) {
            fprintf(stderr, "Invalid reviewed-mask row: %s\n", fields[0]);
            fclose(file);
            destroy_hole_rules(rules);
            return 0;
        }
        fields[7][strcspn(fields[7], "\r\n")] = '\0';
        memset(&rule, 0, sizeof(rule));
        rule.path = strdup(fields[0]);
        rule.expected_width = (size_t)strtoul(fields[1], NULL, 10);
        rule.expected_height = (size_t)strtoul(fields[2], NULL, 10);
        rule.x0 = (size_t)strtoul(fields[3], NULL, 10);
        rule.y0 = (size_t)strtoul(fields[4], NULL, 10);
        rule.x1 = (size_t)strtoul(fields[5], NULL, 10);
        rule.y1 = (size_t)strtoul(fields[6], NULL, 10);
        if (strcmp(fields[7], "alpha") == 0) {
            rule.mode = HOLE_CUT_ALPHA;
        } else if (strcmp(fields[7], "alpha-all") == 0) {
            rule.mode = HOLE_CUT_ALPHA_ALL;
        } else if (strcmp(fields[7], "inpaint") == 0) {
            rule.mode = HOLE_INPAINT_DARK;
        } else {
            fprintf(stderr, "Invalid reviewed-mask mode for %s: %s\n", fields[0], fields[7]);
            free(rule.path);
            fclose(file);
            destroy_hole_rules(rules);
            return 0;
        }

        expanded = realloc(rules->items, (rules->count + 1) * sizeof(*expanded));
        if (rule.path == NULL || expanded == NULL) {
            free(rule.path);
            fclose(file);
            destroy_hole_rules(rules);
            return 0;
        }
        rules->items = expanded;
        rules->items[rules->count++] = rule;
    }

    fclose(file);
    return 1;
}

static int write_pixels(
    MagickWand *wand,
    const uint8_t *rgba,
    size_t width,
    size_t height,
    const char *output_path)
{
    if (!mkdir_parents(output_path)) {
        fprintf(stderr, "Cannot create parent directory for %s\n", output_path);
        return 0;
    }
    if (MagickImportImagePixels(
            wand,
            0,
            0,
            width,
            height,
            "RGBA",
            CharPixel,
            rgba) == MagickFalse) {
        fprintf(stderr, "Cannot import cleaned pixels for %s\n", output_path);
        return 0;
    }
    if (MagickSetImageDepth(wand, 8) == MagickFalse
        || MagickSetImageFormat(wand, "PNG32") == MagickFalse
        || MagickWriteImage(wand, output_path) == MagickFalse) {
        ExceptionType severity;
        char *description = MagickGetException(wand, &severity);
        fprintf(stderr, "Cannot write %s: %s\n", output_path, description);
        description = (char *)MagickRelinquishMemory(description);
        return 0;
    }
    return 1;
}

static int process_file(
    const char *input_path,
    const char *output_path,
    const HoleRules *rules,
    size_t *changed_pixels)
{
    MagickWand *wand = NewMagickWand();
    size_t width;
    size_t height;
    size_t count;
    uint8_t *source;
    uint8_t *output;
    size_t changed;
    size_t changed_alpha = 0;
    size_t changed_hole_rgb = 0;
    size_t rule_index;
    int result = 0;

    if (MagickReadImage(wand, input_path) == MagickFalse) {
        ExceptionType severity;
        char *description = MagickGetException(wand, &severity);
        fprintf(stderr, "Cannot read %s: %s\n", input_path, description);
        description = (char *)MagickRelinquishMemory(description);
        goto cleanup_wand;
    }

    width = MagickGetImageWidth(wand);
    height = MagickGetImageHeight(wand);
    count = width * height;
    source = malloc(count * 4);
    output = malloc(count * 4);
    if (source == NULL || output == NULL) {
        fprintf(stderr, "Out of memory while processing %s\n", input_path);
        free(source);
        free(output);
        goto cleanup_wand;
    }

    if (MagickExportImagePixels(
            wand,
            0,
            0,
            width,
            height,
            "RGBA",
            CharPixel,
            source) == MagickFalse) {
        fprintf(stderr, "Cannot export RGBA pixels from %s\n", input_path);
        goto cleanup_buffers;
    }

    memcpy(output, source, count * 4);
    for (rule_index = 0; rule_index < rules->count; ++rule_index) {
        if (strcmp(rules->items[rule_index].path, input_path) != 0) {
            continue;
        }
        if (!apply_hole_rule(
                output,
                width,
                height,
                &rules->items[rule_index],
                &changed_alpha,
                &changed_hole_rgb)) {
            goto cleanup_buffers;
        }
    }

    changed = clean_boundary(output, source, width, height);
    if (changed == SIZE_MAX) {
        fprintf(stderr, "Cannot allocate distance map for %s\n", input_path);
        goto cleanup_buffers;
    }
    *changed_pixels = changed + changed_hole_rgb + changed_alpha;
    if ((*changed_pixels > 0 || strcmp(input_path, output_path) != 0)
        && !write_pixels(wand, source, width, height, output_path)) {
        goto cleanup_buffers;
    }
    result = 1;

cleanup_buffers:
    free(source);
    free(output);
cleanup_wand:
    wand = DestroyMagickWand(wand);
    return result;
}

static char *trim_line(char *line)
{
    char *end;

    while (isspace((unsigned char)*line)) {
        ++line;
    }
    end = line + strlen(line);
    while (end > line && isspace((unsigned char)end[-1])) {
        --end;
    }
    *end = '\0';
    return line;
}

int main(int argc, char **argv)
{
    const char *manifest_path;
    const char *output_root;
    FILE *manifest;
    char line[8192];
    size_t files = 0;
    size_t total_changed = 0;
    int failed = 0;
    HoleRules rules = {0};

    if (argc != 3 && argc != 4) {
        fprintf(stderr, "Usage: %s MANIFEST OUTPUT_ROOT [REVIEWED_MASKS.csv]\n", argv[0]);
        fprintf(stderr, "Use OUTPUT_ROOT '-' to overwrite manifest paths in place.\n");
        return 2;
    }
    manifest_path = argv[1];
    output_root = argv[2];
    manifest = fopen(manifest_path, "r");
    if (manifest == NULL) {
        fprintf(stderr, "Cannot open manifest %s: %s\n", manifest_path, strerror(errno));
        return 2;
    }
    if (argc == 4 && !load_hole_rules(argv[3], &rules)) {
        fclose(manifest);
        return 2;
    }

    MagickWandGenesis();
    while (fgets(line, sizeof(line), manifest) != NULL) {
        char *input_path = trim_line(line);
        char *output_path;
        size_t changed = 0;
        size_t output_length;

        if (*input_path == '\0' || *input_path == '#') {
            continue;
        }

        if (strcmp(output_root, "-") == 0) {
            output_path = strdup(input_path);
        } else {
            output_length = strlen(output_root) + 1 + strlen(input_path) + 1;
            output_path = malloc(output_length);
            if (output_path != NULL) {
                snprintf(output_path, output_length, "%s/%s", output_root, input_path);
            }
        }
        if (output_path == NULL) {
            fprintf(stderr, "Out of memory while building output path for %s\n", input_path);
            failed = 1;
            break;
        }

        if (!process_file(input_path, output_path, &rules, &changed)) {
            free(output_path);
            failed = 1;
            break;
        }
        printf("%s,%zu\n", input_path, changed);
        fflush(stdout);
        total_changed += changed;
        ++files;
        free(output_path);
    }

    fclose(manifest);
    destroy_hole_rules(&rules);
    MagickWandTerminus();
    fprintf(stderr, "files=%zu changedRGBPixels=%zu failed=%d\n", files, total_changed, failed);
    return failed ? 1 : 0;
}

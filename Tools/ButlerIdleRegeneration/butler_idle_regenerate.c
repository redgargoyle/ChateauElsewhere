#include <MagickWand/MagickWand.h>

#include <errno.h>
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>

enum {
    FRAME_WIDTH = 168,
    FRAME_HEIGHT = 299,
    PLANTED_START_Y = 235,
    CHANNELS = 4,
    FRAME_COUNT = 12
};

static const int kBreathDelta[FRAME_COUNT] = {
    0, 1, 2, 2, 2, 1, 0, -1, -2, -2, -2, -1
};

static uint8_t to_byte(double value)
{
    if (value <= 0.0) {
        return 0;
    }
    if (value >= 255.0) {
        return 255;
    }
    return (uint8_t)lround(value);
}

static void sample_vertical_premultiplied(
    const uint8_t *source,
    size_t x,
    double source_y,
    uint8_t output[CHANNELS])
{
    long y0;
    long y1;
    double fraction;
    double alpha0;
    double alpha1;
    double alpha;
    size_t index0;
    size_t index1;
    int channel;

    if (source_y < 0.0) {
        source_y = 0.0;
    } else if (source_y > FRAME_HEIGHT - 1) {
        source_y = FRAME_HEIGHT - 1;
    }

    y0 = (long)floor(source_y);
    y1 = y0 + 1 < FRAME_HEIGHT ? y0 + 1 : y0;
    fraction = source_y - y0;
    index0 = ((size_t)y0 * FRAME_WIDTH + x) * CHANNELS;
    index1 = ((size_t)y1 * FRAME_WIDTH + x) * CHANNELS;
    alpha0 = source[index0 + 3] / 255.0;
    alpha1 = source[index1 + 3] / 255.0;
    alpha = alpha0 + (alpha1 - alpha0) * fraction;

    output[3] = to_byte(alpha * 255.0);
    if (alpha <= 1.0 / 65535.0) {
        output[0] = 0;
        output[1] = 0;
        output[2] = 0;
        return;
    }

    for (channel = 0; channel < 3; ++channel) {
        const double premultiplied0 = source[index0 + channel] * alpha0;
        const double premultiplied1 = source[index1 + channel] * alpha1;
        const double premultiplied = premultiplied0
            + (premultiplied1 - premultiplied0) * fraction;
        output[channel] = to_byte(premultiplied / alpha);
    }
}

static void regenerate_frame(
    const uint8_t *canonical,
    uint8_t *output,
    int breath_delta)
{
    const double scale = 1.0 + breath_delta / (double)PLANTED_START_Y;
    size_t y;
    size_t x;

    if (breath_delta == 0) {
        memcpy(output, canonical, FRAME_WIDTH * FRAME_HEIGHT * CHANNELS);
        return;
    }

    for (y = 0; y < FRAME_HEIGHT; ++y) {
        for (x = 0; x < FRAME_WIDTH; ++x) {
            const size_t output_index = (y * FRAME_WIDTH + x) * CHANNELS;

            if (y >= PLANTED_START_Y) {
                memcpy(output + output_index, canonical + output_index, CHANNELS);
            } else {
                const double source_y = PLANTED_START_Y
                    - (PLANTED_START_Y - (double)y) / scale;
                sample_vertical_premultiplied(
                    canonical,
                    x,
                    source_y,
                    output + output_index);
            }
        }
    }
}

static int write_frame(
    MagickWand *canonical_wand,
    const uint8_t *pixels,
    const char *path)
{
    MagickWand *output_wand = CloneMagickWand(canonical_wand);
    int success = 0;

    if (output_wand == NULL) {
        fprintf(stderr, "Cannot clone canonical image for %s\n", path);
        return 0;
    }
    if (MagickImportImagePixels(
            output_wand,
            0,
            0,
            FRAME_WIDTH,
            FRAME_HEIGHT,
            "RGBA",
            CharPixel,
            pixels) == MagickFalse
        || MagickSetImageDepth(output_wand, 8) == MagickFalse
        || MagickSetImageFormat(output_wand, "PNG32") == MagickFalse
        || MagickWriteImage(output_wand, path) == MagickFalse) {
        ExceptionType severity;
        char *description = MagickGetException(output_wand, &severity);
        fprintf(stderr, "Cannot write %s: %s\n", path, description);
        description = (char *)MagickRelinquishMemory(description);
    } else {
        success = 1;
    }

    output_wand = DestroyMagickWand(output_wand);
    return success;
}

static size_t count_changed_upper_pixels(
    const uint8_t *canonical,
    const uint8_t *frame)
{
    size_t changed = 0;
    size_t pixel;
    const size_t upper_pixels = FRAME_WIDTH * PLANTED_START_Y;

    for (pixel = 0; pixel < upper_pixels; ++pixel) {
        if (memcmp(
                canonical + pixel * CHANNELS,
                frame + pixel * CHANNELS,
                CHANNELS) != 0) {
            ++changed;
        }
    }
    return changed;
}

static int planted_region_matches(
    const uint8_t *canonical,
    const uint8_t *frame)
{
    const size_t offset = FRAME_WIDTH * PLANTED_START_Y * CHANNELS;
    const size_t byte_count = FRAME_WIDTH
        * (FRAME_HEIGHT - PLANTED_START_Y)
        * CHANNELS;
    return memcmp(canonical + offset, frame + offset, byte_count) == 0;
}

int main(int argc, char **argv)
{
    const char *canonical_path;
    const char *output_directory;
    MagickWand *canonical_wand;
    uint8_t *canonical = NULL;
    uint8_t *frame = NULL;
    size_t frame_index;
    int failed = 0;

    if (argc != 3) {
        fprintf(stderr, "Usage: %s CANONICAL_PNG OUTPUT_DIRECTORY\n", argv[0]);
        return 2;
    }
    canonical_path = argv[1];
    output_directory = argv[2];

    if (mkdir(output_directory, 0775) != 0 && errno != EEXIST) {
        fprintf(stderr, "Cannot create %s: %s\n", output_directory, strerror(errno));
        return 2;
    }

    MagickWandGenesis();
    canonical_wand = NewMagickWand();
    if (MagickReadImage(canonical_wand, canonical_path) == MagickFalse) {
        ExceptionType severity;
        char *description = MagickGetException(canonical_wand, &severity);
        fprintf(stderr, "Cannot read %s: %s\n", canonical_path, description);
        description = (char *)MagickRelinquishMemory(description);
        failed = 1;
        goto cleanup;
    }
    if (MagickGetImageWidth(canonical_wand) != FRAME_WIDTH
        || MagickGetImageHeight(canonical_wand) != FRAME_HEIGHT) {
        fprintf(
            stderr,
            "Canonical frame must be %dx%d, got %zux%zu\n",
            FRAME_WIDTH,
            FRAME_HEIGHT,
            MagickGetImageWidth(canonical_wand),
            MagickGetImageHeight(canonical_wand));
        failed = 1;
        goto cleanup;
    }

    canonical = malloc(FRAME_WIDTH * FRAME_HEIGHT * CHANNELS);
    frame = malloc(FRAME_WIDTH * FRAME_HEIGHT * CHANNELS);
    if (canonical == NULL || frame == NULL) {
        fprintf(stderr, "Out of memory while regenerating Butler idle frames\n");
        failed = 1;
        goto cleanup;
    }
    if (MagickExportImagePixels(
            canonical_wand,
            0,
            0,
            FRAME_WIDTH,
            FRAME_HEIGHT,
            "RGBA",
            CharPixel,
            canonical) == MagickFalse) {
        fprintf(stderr, "Cannot export RGBA pixels from %s\n", canonical_path);
        failed = 1;
        goto cleanup;
    }

    for (frame_index = 0; frame_index < FRAME_COUNT; ++frame_index) {
        char output_path[4096];
        size_t changed_upper;

        regenerate_frame(canonical, frame, kBreathDelta[frame_index]);
        if (!planted_region_matches(canonical, frame)) {
            fprintf(stderr, "Frame %zu changed the planted lower-body region\n", frame_index + 1);
            failed = 1;
            break;
        }
        changed_upper = count_changed_upper_pixels(canonical, frame);
        if ((kBreathDelta[frame_index] == 0 && changed_upper != 0)
            || (kBreathDelta[frame_index] != 0 && changed_upper == 0)) {
            fprintf(stderr, "Frame %zu has an invalid breathing delta\n", frame_index + 1);
            failed = 1;
            break;
        }

        snprintf(
            output_path,
            sizeof(output_path),
            "%s/butler_idle_%02zu.png",
            output_directory,
            frame_index + 1);
        if (!write_frame(canonical_wand, frame, output_path)) {
            failed = 1;
            break;
        }
        printf(
            "%s delta=%d changedUpperPixels=%zu plantedRows=%d-%d\n",
            output_path,
            kBreathDelta[frame_index],
            changed_upper,
            PLANTED_START_Y,
            FRAME_HEIGHT - 1);
    }

cleanup:
    free(canonical);
    free(frame);
    canonical_wand = DestroyMagickWand(canonical_wand);
    MagickWandTerminus();
    return failed ? 1 : 0;
}

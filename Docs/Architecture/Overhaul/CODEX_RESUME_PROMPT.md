# Codex resume prompt

Continue the Chateau Chantilly final architecture overhaul.

1. Read `Docs/Architecture/Overhaul/CODEX_FINISH_ARCHITECTURE_MASTER_PROMPT.md` completely.
2. Read `Docs/Architecture/Overhaul/EXECUTION_STATE.md`.
3. Verify the working tree is clean and HEAD equals the last passing commit recorded there.
4. Verify no second Unity instance has this project open.
5. Resume **only** the exact next slice recorded in `EXECUTION_STATE.md`.
6. Follow the mandatory slice protocol, run real Unity tests with result XML, commit only if every gate passes, then update `EXECUTION_STATE.md`.
7. Continue autonomously through subsequent passing slices until the session limit or a real stop condition.

Do not redo completed slices, increase debt baselines, force Git state, or introduce a parallel owner.

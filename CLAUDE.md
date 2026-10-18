@AGENTS.md

Claude Code: the skills live in `.agents/skills/` as plain markdown and AGENTS.md already
explains how to read them by path. To make Claude Code auto-activate skills from their
description, run once: `mkdir -p .claude/skills && cp -r .agents/skills/* .claude/skills/`
(when editing a skill, edit it under .agents/skills and copy it again).

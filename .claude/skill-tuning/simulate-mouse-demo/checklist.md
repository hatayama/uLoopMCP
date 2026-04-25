# Checklist — simulate-mouse-demo

## Scenario M

- `[critical]` SC.1 self-containment: complete the demo without consulting `uloop-control-play-mode`, `uloop-simulate-mouse-ui`, or `uloop-screenshot` SKILL.md
- `[critical]` Identify all required CLI calls in correct order (screenshot for coordinate discovery → simulate-mouse-ui)
- `[critical]` Cover click, long-press, drag, split-drag, and virtual-pad steps
- `[critical]` State that PlayMode + SimulateMouseDemoScene loaded is the precondition
- `[critical]` Detect any sequencing-semantics gap that a downstream caller would hit
- `[critical]` Self-report all unclear points and discretionary fill-ins

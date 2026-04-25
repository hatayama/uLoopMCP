# Scenarios — uloop-control-play-mode

| ID | Type | Prompt |
|----|------|--------|
| M | median | "Play モードに入ってから simulate-mouse-ui を呼びたい" |

closed-book mode. Median scenario probes both the dispatch (`--action Play`) and the sequencing semantics needed to safely chain into a PlayMode-dependent command. Single scenario is sufficient; the three actions (Play/Stop/Pause) share the same response shape and the sequencing risk is concentrated on Play.

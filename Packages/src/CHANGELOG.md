# Changelog

## [0.45.2](https://github.com/hatayama/uLoopMCP/compare/v1.0.0...v0.45.2) (2026-03-15)


### Features

* (capture-window): rename from capture-unity-window and add MatchMode parameter ([#504](https://github.com/hatayama/uLoopMCP/issues/504)) ([1614fc5](https://github.com/hatayama/uLoopMCP/commit/1614fc564a0680c5f9b68a10c6a741a29b953ff6))
* add --project-path option to CLI for targeting Unity instances by path ([#658](https://github.com/hatayama/uLoopMCP/issues/658)) ([c2073d5](https://github.com/hatayama/uLoopMCP/commit/c2073d5eb53394bbad434a8e8aedc53cbd5d9b74))
* add -d/--delete-recovery option to launch command ([#653](https://github.com/hatayama/uLoopMCP/issues/653)) ([7c2eabe](https://github.com/hatayama/uLoopMCP/commit/7c2eabe7b019de7039543fbe68dfe1965b6fb342))
* Add 'Use Repository Root' toggle for monorepo support ([#308](https://github.com/hatayama/uLoopMCP/issues/308)) ([c958339](https://github.com/hatayama/uLoopMCP/commit/c95833943e454e186eca99d28752f06640436a21))
* add automatic MCP configuration synchronization and enhanced button UI ([#231](https://github.com/hatayama/uLoopMCP/issues/231)) ([bfaf6df](https://github.com/hatayama/uLoopMCP/commit/bfaf6df76b72e6127a589658de36aaa65663f5e3))
* add automatic using directive resolution for CS0246 errors ([#599](https://github.com/hatayama/uLoopMCP/issues/599)) ([aed000f](https://github.com/hatayama/uLoopMCP/commit/aed000fe8c122b7a4e9c6fdb1d86feaf0527d79a))
* add capture-unity-window tool for capturing any EditorWindow ([#471](https://github.com/hatayama/uLoopMCP/issues/471)) ([5f92995](https://github.com/hatayama/uLoopMCP/commit/5f92995d29b226a3738dda77617928c034d45fba))
* add CaptureGameView MCP tool for Unity screenshot capture ([#281](https://github.com/hatayama/uLoopMCP/issues/281)) ([3db45f0](https://github.com/hatayama/uLoopMCP/commit/3db45f01df6ad535ca22a9d81d0e4cebaface9e6))
* add CLI-Unity version mismatch detection ([#466](https://github.com/hatayama/uLoopMCP/issues/466)) ([df1a92f](https://github.com/hatayama/uLoopMCP/commit/df1a92f70bbf45ba6a37914cbb2f18997bad4af9))
* add control-play-mode tool and fix MainThreadSwitcher for pause state ([#468](https://github.com/hatayama/uLoopMCP/issues/468)) ([35ee074](https://github.com/hatayama/uLoopMCP/commit/35ee0745f4e82ba6cee5c5148f9500f1bdf983c5))
* Add delete button for MCP configuration ([#718](https://github.com/hatayama/uLoopMCP/issues/718)) ([3e4c4fb](https://github.com/hatayama/uLoopMCP/commit/3e4c4fb907b1fc446e19f3eba98bf6afc174a3ee))
* Add domain-reload wait option for compile tool with file-based recovery ([#650](https://github.com/hatayama/uLoopMCP/issues/650)) ([5536b1e](https://github.com/hatayama/uLoopMCP/commit/5536b1e2a8105710045d4b8320a1d8a944ab589c))
* Add dynamic code execution with multi-level security framework ([#286](https://github.com/hatayama/uLoopMCP/issues/286)) ([8ad24de](https://github.com/hatayama/uLoopMCP/commit/8ad24de34a573909ecbb2e0fad25e8cd7fa62837))
* add macOS focus-window MCP tool ([#337](https://github.com/hatayama/uLoopMCP/issues/337)) ([0fc217a](https://github.com/hatayama/uLoopMCP/commit/0fc217af20447b1798c12ff4d2a55c6ebe8df016))
* Add MCP configuration auto-update utility before server startup ([#349](https://github.com/hatayama/uLoopMCP/issues/349)) ([1d510d1](https://github.com/hatayama/uLoopMCP/commit/1d510d1d6648a4430414041cddc8326626c7112e))
* Add MCP keepalive service to prevent Cursor idle timeout ([#381](https://github.com/hatayama/uLoopMCP/issues/381)) ([cbdce47](https://github.com/hatayama/uLoopMCP/commit/cbdce4732cf36a01384df4eea362545f693822bf))
* Add per-tool enable/disable toggle for MCP tools ([#698](https://github.com/hatayama/uLoopMCP/issues/698)) ([5ca8b4c](https://github.com/hatayama/uLoopMCP/commit/5ca8b4ca99ed618148a93d74a9dd76fb6f0cff60))
* Add Prefab Stage support and ObjectReference serialization to find-game-objects ([#636](https://github.com/hatayama/uLoopMCP/issues/636)) ([f667aa8](https://github.com/hatayama/uLoopMCP/commit/f667aa81ec69327b383c28722173f846085a3dc3))
* add regex and stack trace search support to get-logs tool ([#207](https://github.com/hatayama/uLoopMCP/issues/207)) ([745135a](https://github.com/hatayama/uLoopMCP/commit/745135a67f678734c911f07be73a486bc29324c2))
* Add Selected mode to FindGameObjects tool ([#569](https://github.com/hatayama/uLoopMCP/issues/569)) ([d2f50cd](https://github.com/hatayama/uLoopMCP/commit/d2f50cd4c4a9b375dc1ce7088648e55f25c0ee20))
* Add simulate-mouse tool for PlayMode UI interaction ([#759](https://github.com/hatayama/uLoopMCP/issues/759)) ([5679f34](https://github.com/hatayama/uLoopMCP/commit/5679f342932a67aecefbff804bd7265fdd6ec00d))
* add standalone uloop CLI with shell completion and bundled skills ([#443](https://github.com/hatayama/uLoopMCP/issues/443)) ([34f01cb](https://github.com/hatayama/uLoopMCP/commit/34f01cb1f243fc88900aef40a75cd60f36437173))
* Add UseSelection mode to GetHierarchy tool ([#575](https://github.com/hatayama/uLoopMCP/issues/575)) ([1bdb079](https://github.com/hatayama/uLoopMCP/commit/1bdb0791a321b819800b52f87d1016f21698e782))
* Add version mismatch diagnostic for timeout and connection errors ([#576](https://github.com/hatayama/uLoopMCP/issues/576)) ([a4c9125](https://github.com/hatayama/uLoopMCP/commit/a4c9125830e253442c9fd464533dd48585f077e7))
* add Windows npm install permission pre-check and error classification ([#677](https://github.com/hatayama/uLoopMCP/issues/677)) ([d73077c](https://github.com/hatayama/uLoopMCP/commit/d73077c3ee771162043557a4172783bacd2f24a6))
* Add Windows support for focus-window tool ([#338](https://github.com/hatayama/uLoopMCP/issues/338)) ([101267d](https://github.com/hatayama/uLoopMCP/commit/101267dcb594e8324bdea3c88c2163a9e5627561))
* align Codex classification and add regression tests after [#609](https://github.com/hatayama/uLoopMCP/issues/609) ([#614](https://github.com/hatayama/uLoopMCP/issues/614)) ([4eb2432](https://github.com/hatayama/uLoopMCP/commit/4eb24325a5fa7a6a37d498294eadb9f961533efd))
* async-aware execute-dynamic-code pipeline ([#316](https://github.com/hatayama/uLoopMCP/issues/316)) ([2c699a9](https://github.com/hatayama/uLoopMCP/commit/2c699a93634e6f0371586bfb1408b7036c56191e))
* change output directory from uLoopMCPOutputs/ to .uloop/outputs/ ([#490](https://github.com/hatayama/uLoopMCP/issues/490)) ([30de345](https://github.com/hatayama/uLoopMCP/commit/30de345ac93f869ec5adb3949fd8ed38b7ed2628))
* **cli:** Add launch command to open Unity projects ([#577](https://github.com/hatayama/uLoopMCP/issues/577)) ([d91c153](https://github.com/hatayama/uLoopMCP/commit/d91c153f5f2985c06d522e96253879630c5f757f))
* **cli:** add multi-target support for skills command ([#455](https://github.com/hatayama/uLoopMCP/issues/455)) ([9f7e8ab](https://github.com/hatayama/uLoopMCP/commit/9f7e8abd3c8780d7685a73e466de1b871b52d50f))
* **cli:** add SKILL.md auto-collection and project skills support ([#486](https://github.com/hatayama/uLoopMCP/issues/486)) ([f9aee30](https://github.com/hatayama/uLoopMCP/commit/f9aee305ac14c6e18eba799dabd747a05fdfb660))
* **cli:** auto-sync tools cache when CLI version changes ([#562](https://github.com/hatayama/uLoopMCP/issues/562)) ([cdb014d](https://github.com/hatayama/uLoopMCP/commit/cdb014d955077ca21c981b112f3c679533e66ae9))
* **cli:** improve error messages and UX during Unity busy states ([#453](https://github.com/hatayama/uLoopMCP/issues/453)) ([2927553](https://github.com/hatayama/uLoopMCP/commit/2927553e0f5143a6fb16b1197c767297219973a0))
* **cli:** search child directories for Unity projects with uLoopMCP ([#510](https://github.com/hatayama/uLoopMCP/issues/510)) ([b83d12a](https://github.com/hatayama/uLoopMCP/commit/b83d12a316cd685093c107da7a9b5fb63a4bc72d))
* **compile:** Respect Unity's Script Changes While Playing setting ([#582](https://github.com/hatayama/uLoopMCP/issues/582)) ([d2e0659](https://github.com/hatayama/uLoopMCP/commit/d2e06596e86ae47ac20dff3e0c21ba1378b8f9b9))
* **Editor:** add Codex MCP TOML config service and update Editor UI ([#292](https://github.com/hatayama/uLoopMCP/issues/292)) ([612fc70](https://github.com/hatayama/uLoopMCP/commit/612fc7032d39339557cc6db2c88469533db813a3))
* Enable Codex configuration on Windows ([#331](https://github.com/hatayama/uLoopMCP/issues/331)) ([1ece6d7](https://github.com/hatayama/uLoopMCP/commit/1ece6d7f1035a16d8df7ba9dc147a87085c664b6))
* **execute-dynamic-code:** wrap execution in single Undo group ([#507](https://github.com/hatayama/uLoopMCP/issues/507)) ([3402d41](https://github.com/hatayama/uLoopMCP/commit/3402d4132e93021507e39ad2a92f56561f697e2e))
* Implement focus-window at OS level using launch-unity package ([#580](https://github.com/hatayama/uLoopMCP/issues/580)) ([978896d](https://github.com/hatayama/uLoopMCP/commit/978896db7188d81b0d99fe692529fc699d6dadfa))
* improve CLI integration UI and replace Auto Start with server state restoration ([#664](https://github.com/hatayama/uLoopMCP/issues/664)) ([c9ca4d7](https://github.com/hatayama/uLoopMCP/commit/c9ca4d79cb08390e0a07273b107c277b685b3eb7))
* improve EditorWindow UI — merge server sections, compact controls, add CLI/MCP tabs ([#662](https://github.com/hatayama/uLoopMCP/issues/662)) ([3301d8c](https://github.com/hatayama/uLoopMCP/commit/3301d8cb3551f748ab6f5658ed1a006c3a148eac))
* improve hierarchy export and RootPath matching ([#314](https://github.com/hatayama/uLoopMCP/issues/314)) ([8be0e18](https://github.com/hatayama/uLoopMCP/commit/8be0e189ae6b60d78f97c3369ca6adf988b33efe))
* improve skill discoverability with better naming and descriptions ([#534](https://github.com/hatayama/uLoopMCP/issues/534)) ([0c9c2d5](https://github.com/hatayama/uLoopMCP/commit/0c9c2d5a02793e9b4ee7b6f7e4043a6045fbd8c3))
* Migrate security settings to project-scoped .uloop/settings.security.json ([#696](https://github.com/hatayama/uLoopMCP/issues/696)) ([222d46e](https://github.com/hatayama/uLoopMCP/commit/222d46e485b5b0006f8ed9e74e19e191938f2010))
* migrate skills to Agent Skills specification structure ([#538](https://github.com/hatayama/uLoopMCP/issues/538)) ([64dea12](https://github.com/hatayama/uLoopMCP/commit/64dea12d0887e1f1366a7b8ee8a82852fc57fb2d))
* prevent Cursor MCP disconnection on package updates with stable server path ([#386](https://github.com/hatayama/uLoopMCP/issues/386)) ([171a1a9](https://github.com/hatayama/uLoopMCP/commit/171a1a9d836302b649dd9c7a9e5cfac48f58ce61))
* Rebrand to Unity CLI Loop, bump to v1.0.0 ([#769](https://github.com/hatayama/uLoopMCP/issues/769)) ([34081c5](https://github.com/hatayama/uLoopMCP/commit/34081c58e8206e68264720c4a8b63683c71b8a0d))
* restore and enhance security settings with third-party tool control ([#217](https://github.com/hatayama/uLoopMCP/issues/217)) ([144c04d](https://github.com/hatayama/uLoopMCP/commit/144c04ddc4275c7f6a4e2a3e52e02e54a2326c55))
* return success message for pending requests on temporary disconnect ([#392](https://github.com/hatayama/uLoopMCP/issues/392)) ([b2bfa6d](https://github.com/hatayama/uLoopMCP/commit/b2bfa6d497a86f9809e88ff70282de640f1ac4c8))
* **skills:** add progressive disclosure and expand execute-dynamic-code examples ([#506](https://github.com/hatayama/uLoopMCP/issues/506)) ([b63fd13](https://github.com/hatayama/uLoopMCP/commit/b63fd134b93cbad9d660d7db738b15e6aec99044))
* skip server auto-start on first launch ([#390](https://github.com/hatayama/uLoopMCP/issues/390)) ([3b320eb](https://github.com/hatayama/uLoopMCP/commit/3b320eb8cad2c540e50f041a846431714ea03be9))
* support CS0103 auto-using resolution ([#641](https://github.com/hatayama/uLoopMCP/issues/641)) ([a05edf7](https://github.com/hatayama/uLoopMCP/commit/a05edf7e36e055d76b154df5f35ceb691f724a20))
* Switch CLI skills to dynamic package loading ([#537](https://github.com/hatayama/uLoopMCP/issues/537)) ([4e48593](https://github.com/hatayama/uLoopMCP/commit/4e48593f81d76f62c764a88bffbfde56c0dafa35))
* trigger minor release ([#302](https://github.com/hatayama/uLoopMCP/issues/302)) ([b652ac1](https://github.com/hatayama/uLoopMCP/commit/b652ac174c8662bb73395ec3f43f1e22e63cc6dd))
* **ui:** migrate McpEditorWindow from IMGUI to UI Toolkit ([#503](https://github.com/hatayama/uLoopMCP/issues/503)) ([c901024](https://github.com/hatayama/uLoopMCP/commit/c9010249f4d4ad33930adbcc08a331c20d4ed701))
* upgrade launch-unity to v0.15.0 and use orchestrateLaunch() ([#601](https://github.com/hatayama/uLoopMCP/issues/601)) ([70c64d7](https://github.com/hatayama/uLoopMCP/commit/70c64d75f73f4ab0f693fb27a5eb6231d74345ee))
* verify Node.js availability before MCP configuration ([#388](https://github.com/hatayama/uLoopMCP/issues/388)) ([3bf2c29](https://github.com/hatayama/uLoopMCP/commit/3bf2c29208bac74820ce48bc61df4b7cd08a5181))


### Bug Fixes

* (execute-dynamic-code)  return optional, better Parameters guidance, and test ([#289](https://github.com/hatayama/uLoopMCP/issues/289)) ([3d8f954](https://github.com/hatayama/uLoopMCP/commit/3d8f954a5a9d2374b79635706bc15b6c2b512221))
* Add Directory.Exists check before creating config directory ([#345](https://github.com/hatayama/uLoopMCP/issues/345)) ([ac14c61](https://github.com/hatayama/uLoopMCP/commit/ac14c61f4da976398e9855b398f8eabb57534bfa))
* add fallback when node validation fails ([#470](https://github.com/hatayama/uLoopMCP/issues/470)) ([9e4bd14](https://github.com/hatayama/uLoopMCP/commit/9e4bd1499f19cf44792a8d9b4017cc6732d46e15))
* Add GetVersionTool to core package and improve CLI error handling ([#723](https://github.com/hatayama/uLoopMCP/issues/723)) ([21afcc8](https://github.com/hatayama/uLoopMCP/commit/21afcc899d7b8857737a7bda9480ccb447f9083c))
* add meta ([#692](https://github.com/hatayama/uLoopMCP/issues/692)) ([34b29de](https://github.com/hatayama/uLoopMCP/commit/34b29de1ea497e8755f6c8cf711eebf7b1378f15))
* add README for uloop-cli npm package ([#541](https://github.com/hatayama/uLoopMCP/issues/541)) ([b8ec5b5](https://github.com/hatayama/uLoopMCP/commit/b8ec5b5f1418847a32bc8d9b6764b85230adf24a))
* Add uloop fix suggestion to error messages ([#593](https://github.com/hatayama/uLoopMCP/issues/593)) ([ccca327](https://github.com/hatayama/uLoopMCP/commit/ccca327ca13996602c3fbe2d4e1c20dc2a8f0ab4))
* apply context: fork to uloop-execute-dynamic-code skill and add wiring references ([#743](https://github.com/hatayama/uLoopMCP/issues/743)) ([4ab452b](https://github.com/hatayama/uLoopMCP/commit/4ab452ba9efde76704b18d58d403761df2128941))
* Auto Configuration File Path Updates During Auto Start Server Recovery ([#351](https://github.com/hatayama/uLoopMCP/issues/351)) ([013478f](https://github.com/hatayama/uLoopMCP/commit/013478f210550c69d25589a3531c08f805440d22))
* auto-detect Unity project root from subdirectories ([#445](https://github.com/hatayama/uLoopMCP/issues/445)) ([d11635e](https://github.com/hatayama/uLoopMCP/commit/d11635e7cefb27ecc3a9bb00475e4361c50152e4))
* auto-rebuild server.bundle.js on release and upgrade Node.js to v22 ([#424](https://github.com/hatayama/uLoopMCP/issues/424)) ([24f26ff](https://github.com/hatayama/uLoopMCP/commit/24f26ff8a2732f43dd19ceaa73f85b39cee95e32))
* Auto-save NUnit XML on test failure ([#600](https://github.com/hatayama/uLoopMCP/issues/600)) ([defa488](https://github.com/hatayama/uLoopMCP/commit/defa488b6ee3acf3a15a7558c40ac7ac69bf1b99))
* change CLI boolean options to value format for MCP consistency ([#559](https://github.com/hatayama/uLoopMCP/issues/559)) ([3ad62e6](https://github.com/hatayama/uLoopMCP/commit/3ad62e677f92e39f50bf55606f8aef539678d083))
* change IncludeStackTrace default value from true to false in get-logs ([#557](https://github.com/hatayama/uLoopMCP/issues/557)) ([8609dbc](https://github.com/hatayama/uLoopMCP/commit/8609dbc71ca9d37fa11053e95450a6702d89cf3a))
* Classify csc.rsp compiler diagnostics correctly in get-logs LogType filter ([#761](https://github.com/hatayama/uLoopMCP/issues/761)) ([#767](https://github.com/hatayama/uLoopMCP/issues/767)) ([7069c5f](https://github.com/hatayama/uLoopMCP/commit/7069c5f5a0b7d646060200373515d65fa8d24809))
* clear pending requests on socket close and reduce timeout to 3 minutes ([#405](https://github.com/hatayama/uLoopMCP/issues/405)) ([7dbc7e6](https://github.com/hatayama/uLoopMCP/commit/7dbc7e6a207c486f44e0397593be977d3f556d11))
* **cli:** display version change in update command ([#478](https://github.com/hatayama/uLoopMCP/issues/478)) ([74c948b](https://github.com/hatayama/uLoopMCP/commit/74c948bb6d6239eaf58aeb5e5ded9eb04d1e2ca4))
* **cli:** improve skills command guidance message ([#456](https://github.com/hatayama/uLoopMCP/issues/456)) ([baa0282](https://github.com/hatayama/uLoopMCP/commit/baa0282cdf77a0feebe89fdf88a41a3c7e3e5305))
* **cli:** resolve lint warnings and type errors ([#524](https://github.com/hatayama/uLoopMCP/issues/524)) ([ffe0df6](https://github.com/hatayama/uLoopMCP/commit/ffe0df616a204991895c5940f593606c23bdd128))
* **cli:** trigger release for npm publish workflow fix ([#448](https://github.com/hatayama/uLoopMCP/issues/448)) ([9603e9f](https://github.com/hatayama/uLoopMCP/commit/9603e9f9f168fd0d23b503c64689b815774c0240))
* **cli:** update tsx to 4.21.0 to fix esbuild vulnerability ([#496](https://github.com/hatayama/uLoopMCP/issues/496)) ([e7240e2](https://github.com/hatayama/uLoopMCP/commit/e7240e221fa1771d7bb7c28e835f61c2f6e9b2b4))
* Codexのプロジェクト単位設定をサポート ([#609](https://github.com/hatayama/uLoopMCP/issues/609)) ([3e99d97](https://github.com/hatayama/uLoopMCP/commit/3e99d97b967e04a8541d50c2b619f9817a11194b))
* **compile:** report duplicate asmdef and avoid silent hangs ([#529](https://github.com/hatayama/uLoopMCP/issues/529)) ([f181aaa](https://github.com/hatayama/uLoopMCP/commit/f181aaaecfdc28c148bbbc371bbf5d969cb96081))
* **config:** preserve non-uLoopMCP servers when saving MCP configuration ([#518](https://github.com/hatayama/uLoopMCP/issues/518)) ([635ece7](https://github.com/hatayama/uLoopMCP/commit/635ece7d7096834cb2e6b88ed488aee25e3e0dfc))
* configure release-please v4 to sync TypeScript version ([#416](https://github.com/hatayama/uLoopMCP/issues/416)) ([11b0951](https://github.com/hatayama/uLoopMCP/commit/11b09512c678ebedcbd5e0ef19db71260741a905))
* consolidate serverPort and customPort into single customPort field ([#666](https://github.com/hatayama/uLoopMCP/issues/666)) ([1ffcbdf](https://github.com/hatayama/uLoopMCP/commit/1ffcbdfc042431a759b6eef8c9e8901ff067d07d))
* convert UTF-8 byte position to character position in get-logs ([#555](https://github.com/hatayama/uLoopMCP/issues/555)) ([04aaaff](https://github.com/hatayama/uLoopMCP/commit/04aaaffef518ad1447ca2de6a48234d0cc0eb899))
* correct SKILL.md file format ([#535](https://github.com/hatayama/uLoopMCP/issues/535)) ([79ce5d4](https://github.com/hatayama/uLoopMCP/commit/79ce5d4cd1936f53a3c5f84a21e886f8a925eabd))
* correct SKILL.md parameter docs to match C# implementations ([#689](https://github.com/hatayama/uLoopMCP/issues/689)) ([e76ab29](https://github.com/hatayama/uLoopMCP/commit/e76ab29372c28f6513016d11b9fb7cb18e6f025f))
* delay mcp.json update until Start Server button is pressed ([#493](https://github.com/hatayama/uLoopMCP/issues/493)) ([86d94bb](https://github.com/hatayama/uLoopMCP/commit/86d94bbbce8986a70b94f10039a22973edcd3387))
* Delete lock files when server startup is skipped ([#595](https://github.com/hatayama/uLoopMCP/issues/595)) ([9621cc2](https://github.com/hatayama/uLoopMCP/commit/9621cc21522f0cf43de6013a5d69c4e3891b4d12))
* Delete Unnecessary Files ([#543](https://github.com/hatayama/uLoopMCP/issues/543)) ([cec1615](https://github.com/hatayama/uLoopMCP/commit/cec1615b1d1ad9672f98ca0e731d0fe6f607939a))
* **deps:** bump launch-unity to 0.15.1 ([c25653a](https://github.com/hatayama/uLoopMCP/commit/c25653a590597c2702e13943e9595d71c1b51e73))
* **deps:** upgrade eslint to v10 and resolve minimatch ReDoS vulnerability ([#660](https://github.com/hatayama/uLoopMCP/issues/660)) ([11e4b8b](https://github.com/hatayama/uLoopMCP/commit/11e4b8b0cbd80454f52f5ea9586c52a139cb73d9))
* detect Unity project root dynamically for VibeLogger ([dd5568c](https://github.com/hatayama/uLoopMCP/commit/dd5568c9189f47d1d1b67d668867ea6095fa154e))
* disable Skills subsection when CLI is not installed ([#681](https://github.com/hatayama/uLoopMCP/issues/681)) ([3814154](https://github.com/hatayama/uLoopMCP/commit/381415449281f955e7c302d56c2a52e2db97dc2b))
* distinguish Downgrade CLI from Update CLI in version mismatch display ([#668](https://github.com/hatayama/uLoopMCP/issues/668)) ([28c8ead](https://github.com/hatayama/uLoopMCP/commit/28c8eadf877114a8993c73f104a64a3258f5656d))
* domain reload connection stability and eliminate duplicate tool connections ([#220](https://github.com/hatayama/uLoopMCP/issues/220)) ([c983ed0](https://github.com/hatayama/uLoopMCP/commit/c983ed0fbbe24145e2834d98e6d2f4f6bb19fe3c))
* **editor:** rename InternalAPIEditorBridge from 001 to 024 ([#484](https://github.com/hatayama/uLoopMCP/issues/484)) ([5be11ea](https://github.com/hatayama/uLoopMCP/commit/5be11ead3e74631d2e696fdd61a84ca35225963d))
* **EditorUI:** eliminate Texture2D background leak in MCP editor window ([#296](https://github.com/hatayama/uLoopMCP/issues/296)) ([a95ce3f](https://github.com/hatayama/uLoopMCP/commit/a95ce3f7446c66ad4ef99ded16da8497adef58f2))
* eliminate 'No connected tools found' flash with event-driven architecture ([#229](https://github.com/hatayama/uLoopMCP/issues/229)) ([8fc99f7](https://github.com/hatayama/uLoopMCP/commit/8fc99f73c760bd0d07c665d2b2bb79f60c84869e))
* Enforce mandatory UNITY_TCP_PORT environment variable validation ([#213](https://github.com/hatayama/uLoopMCP/issues/213)) ([415b834](https://github.com/hatayama/uLoopMCP/commit/415b8349518c60a7176fb4f603f8e17091d85872))
* Enhance Connected Tools UI with Port Display and Better Contrast ([#268](https://github.com/hatayama/uLoopMCP/issues/268)) ([04589f6](https://github.com/hatayama/uLoopMCP/commit/04589f616a474dd4c448a9c24a9acd273d4568b7))
* Enhance supply chain security and dependency management ([#361](https://github.com/hatayama/uLoopMCP/issues/361)) ([1cb2165](https://github.com/hatayama/uLoopMCP/commit/1cb2165abbb9f118801cac8165fce615e8e8ba83))
* ensure server recovery after domain reload when instance is null ([#551](https://github.com/hatayama/uLoopMCP/issues/551)) ([ad5abc6](https://github.com/hatayama/uLoopMCP/commit/ad5abc62410ed8e8503d8fbecbf9332f6f6ecba8))
* Ensure server.bundle.js is committed on all PRs ([#597](https://github.com/hatayama/uLoopMCP/issues/597)) ([7f55387](https://github.com/hatayama/uLoopMCP/commit/7f5538717284300f59eab845e9294a1f3833693d))
* Ensure Unity client retries after initial socket failure ([#306](https://github.com/hatayama/uLoopMCP/issues/306)) ([04cba58](https://github.com/hatayama/uLoopMCP/commit/04cba5887de3647d9622a14136abc2ae33e90fbc))
* Fix CLI port resolution when serverPort is invalid ([#619](https://github.com/hatayama/uLoopMCP/issues/619)) ([8ddc4a5](https://github.com/hatayama/uLoopMCP/commit/8ddc4a5222f624571fc10912269f18f8b2f86bae))
* Fix incomplete property serialization in ComponentPropertySerializer ([#691](https://github.com/hatayama/uLoopMCP/issues/691)) ([2db6eb3](https://github.com/hatayama/uLoopMCP/commit/2db6eb3f63865ab6e56c41cabbbe3a37d183b164))
* fix Unity window focus functionality and add test script ([#412](https://github.com/hatayama/uLoopMCP/issues/412)) ([b9293e9](https://github.com/hatayama/uLoopMCP/commit/b9293e905343a4ee66ec0332cdf08be16b309121))
* **focus-window:** remove platform-specific preprocessor directives ([#513](https://github.com/hatayama/uLoopMCP/issues/513)) ([bcf1894](https://github.com/hatayama/uLoopMCP/commit/bcf18945d4c7164375dcc304bcc3197753e7339e))
* Group help commands by category in uloop -h ([#708](https://github.com/hatayama/uLoopMCP/issues/708)) ([dbfe539](https://github.com/hatayama/uLoopMCP/commit/dbfe5394172a80f550304b95787d36d4046d0404))
* handle Dictionary type as object in schema and CLI value conversion ([#564](https://github.com/hatayama/uLoopMCP/issues/564)) ([2047ba9](https://github.com/hatayama/uLoopMCP/commit/2047ba93febbf90f7a41b228ad8631f6ddf3ca91))
* handle duplicate MenuItem attributes with improved warnings ([#283](https://github.com/hatayama/uLoopMCP/issues/283)) ([3c7b98a](https://github.com/hatayama/uLoopMCP/commit/3c7b98a0e7ffba33d9a9c06b7c7947c21fc941ee))
* handle JSON array format in CLI default values ([#560](https://github.com/hatayama/uLoopMCP/issues/560)) ([e194347](https://github.com/hatayama/uLoopMCP/commit/e1943478d7faea0a5f01adc4d8b8f0b63a75cd5e))
* Handle Win32Exception in skills install process start ([#730](https://github.com/hatayama/uLoopMCP/issues/730)) ([cd7a4a3](https://github.com/hatayama/uLoopMCP/commit/cd7a4a3a19ab1d4830d1131074caeab4e85969e1))
* Hide disabled tools from CLI help and shell completion output ([#705](https://github.com/hatayama/uLoopMCP/issues/705)) ([3f49750](https://github.com/hatayama/uLoopMCP/commit/3f4975078e324b56e4363c510c6081b7373afa63))
* improve capture-unity-window quality and simplify code ([#473](https://github.com/hatayama/uLoopMCP/issues/473)) ([71debb4](https://github.com/hatayama/uLoopMCP/commit/71debb4477f321400526c2f8ffb2d94411b53a3d))
* improve CLI connection error message for better user experience ([#544](https://github.com/hatayama/uLoopMCP/issues/544)) ([ac62293](https://github.com/hatayama/uLoopMCP/commit/ac622936a574bd15d599478c61614d8b93fc3d5d))
* Improve connection error message with post-compile guidance ([#304](https://github.com/hatayama/uLoopMCP/issues/304)) ([c04921d](https://github.com/hatayama/uLoopMCP/commit/c04921d2655435283ba89f03d008b4d7c91fbeed))
* improve disconnected messages based on Unity shutdown reason ([#353](https://github.com/hatayama/uLoopMCP/issues/353)) ([d23034e](https://github.com/hatayama/uLoopMCP/commit/d23034e7eb19b516af54123f846d8739c6856ff1))
* Improve error handling and simplify tool response messages ([#340](https://github.com/hatayama/uLoopMCP/issues/340)) ([a3c6dd2](https://github.com/hatayama/uLoopMCP/commit/a3c6dd2740bfd07c2b0a8adfbc0f4467de02374e))
* improve reconnection speed after Unity domain reload ([#211](https://github.com/hatayama/uLoopMCP/issues/211)) ([b7e173c](https://github.com/hatayama/uLoopMCP/commit/b7e173c56bfa588471f0c3e6f7f6b59567cf06bf))
* improve server recovery fallback when saved port is unavailable ([#639](https://github.com/hatayama/uLoopMCP/issues/639)) ([1ee5e6c](https://github.com/hatayama/uLoopMCP/commit/1ee5e6ce97b2f88668d61743d9037c6d6664ffbf))
* improve skill descriptions for better AI recognition ([#553](https://github.com/hatayama/uLoopMCP/issues/553)) ([3febc3a](https://github.com/hatayama/uLoopMCP/commit/3febc3affb9e0c63dc98294ec508fa909ed8bb9b))
* Improve temporary disconnection handling for better LLM interaction ([#343](https://github.com/hatayama/uLoopMCP/issues/343)) ([80dc21a](https://github.com/hatayama/uLoopMCP/commit/80dc21aa235517850387b4572bef45f3f27056e6))
* improve timeout error message to prevent AI from killing Unity processes ([#567](https://github.com/hatayama/uLoopMCP/issues/567)) ([208c0e0](https://github.com/hatayama/uLoopMCP/commit/208c0e0167e6c44ff5a72da82c612321b5ec162c))
* Improve tool settings UI message to mention context window benefit ([#700](https://github.com/hatayama/uLoopMCP/issues/700)) ([040c8bd](https://github.com/hatayama/uLoopMCP/commit/040c8bd8687d8925219285f3b401c3d6900a5f3e))
* improve UI responsiveness and port validation behavior ([#235](https://github.com/hatayama/uLoopMCP/issues/235)) ([6e11384](https://github.com/hatayama/uLoopMCP/commit/6e1138446f23660f02110b8a40d7b1c54bc651ca))
* improve Windows error message clarity and remove CLI install success dialog ([#678](https://github.com/hatayama/uLoopMCP/issues/678)) ([9052eba](https://github.com/hatayama/uLoopMCP/commit/9052ebaea8afa0c580295302a7ae75d190ebc3ee))
* increase network timeout to 5min and improve timeout error message ([#393](https://github.com/hatayama/uLoopMCP/issues/393)) ([cc95840](https://github.com/hatayama/uLoopMCP/commit/cc9584093f8ee3aee93bbe34437fc2189d0363eb))
* isolate Roslyn dependencies via shared registry ([#741](https://github.com/hatayama/uLoopMCP/issues/741)) ([c96a3a2](https://github.com/hatayama/uLoopMCP/commit/c96a3a2e06a268cc4daa5cb0eb9936b638abf5bb))
* keep TCP port stable after crash recovery ([#335](https://github.com/hatayama/uLoopMCP/issues/335)) ([e2df9cf](https://github.com/hatayama/uLoopMCP/commit/e2df9cf348e9a8e1eba33e18654575740b120a94))
* make mcp.json equality check order-insensitive to avoid needless rewrites ([#293](https://github.com/hatayama/uLoopMCP/issues/293)) ([9e8444d](https://github.com/hatayama/uLoopMCP/commit/9e8444d5bcf964b160ef2a6ab40dacecbc01b613))
* MCP client name display and Unity connection timeout issues ([#222](https://github.com/hatayama/uLoopMCP/issues/222)) ([e002763](https://github.com/hatayama/uLoopMCP/commit/e00276380dc534855296fa7a12590b8cecd6e730))
* normalize get-logs error filtering and harden CLI e2e parsing ([#643](https://github.com/hatayama/uLoopMCP/issues/643)) ([cc42ba9](https://github.com/hatayama/uLoopMCP/commit/cc42ba9f98e4c01ba82b7e7e79e39a826d674c30))
* Optimize response JSON and add Claude Code skills ([#476](https://github.com/hatayama/uLoopMCP/issues/476)) ([c5d17c4](https://github.com/hatayama/uLoopMCP/commit/c5d17c4e226170a0edb625788c0bbed1f7d295a9))
* prevent background Unity processes from mutating server state ([#642](https://github.com/hatayama/uLoopMCP/issues/642)) ([cdf1285](https://github.com/hatayama/uLoopMCP/commit/cdf1285f41165fec080227139b777f6b6fff83a8))
* Prevent CLI from sending commands to wrong Unity instance ([#719](https://github.com/hatayama/uLoopMCP/issues/719)) ([d0e47b3](https://github.com/hatayama/uLoopMCP/commit/d0e47b392677af36dfba740e9c7e80579877bad8))
* prevent Cursor MCP disconnection by fixing initialization race conditions ([#376](https://github.com/hatayama/uLoopMCP/issues/376)) ([220e553](https://github.com/hatayama/uLoopMCP/commit/220e5532561fc9ac0b6e5d843af5c08da8d305b3))
* prevent false port conflict dialog by awaiting recovery task ([#441](https://github.com/hatayama/uLoopMCP/issues/441)) ([75512a2](https://github.com/hatayama/uLoopMCP/commit/75512a219f10f3c64bf62e3545ddef8a15578389))
* prevent JSON corruption from concurrent SaveSettings writes ([#603](https://github.com/hatayama/uLoopMCP/issues/603)) ([763addc](https://github.com/hatayama/uLoopMCP/commit/763addcf088731fd05bc7997705d1a53acfb8d54))
* prevent port conflict dialog during domain reload recovery ([#383](https://github.com/hatayama/uLoopMCP/issues/383)) ([0456c75](https://github.com/hatayama/uLoopMCP/commit/0456c7535d9df290207ca64e7c0ab725f4aceeee))
* Prevent rename migration from shadowing legacy settings migration ([#725](https://github.com/hatayama/uLoopMCP/issues/725)) ([b03337b](https://github.com/hatayama/uLoopMCP/commit/b03337be8892cc807e8f6a47beab023c7d985bc9))
* prevent timeout from affecting unrelated pending requests ([#401](https://github.com/hatayama/uLoopMCP/issues/401)) ([2a533fc](https://github.com/hatayama/uLoopMCP/commit/2a533fc8627d523b3c49ac133d2761aeb9219cf0))
* Prevent toggle ChangeEvent from collapsing parent Foldout ([#721](https://github.com/hatayama/uLoopMCP/issues/721)) ([f6caf9b](https://github.com/hatayama/uLoopMCP/commit/f6caf9b7066f5f4587788c7e3b2d9e8ce0062aa3))
* prioritize .cmd/.exe over extensionless shims in Windows executable detection ([#669](https://github.com/hatayama/uLoopMCP/issues/669)) ([25fc66b](https://github.com/hatayama/uLoopMCP/commit/25fc66bb2462fb1fb6643b84e449de3c0be1a20e))
* recover from stuck connection state after long-term operation ([#389](https://github.com/hatayama/uLoopMCP/issues/389)) ([1244a5d](https://github.com/hatayama/uLoopMCP/commit/1244a5d7d108b08d0e78c210e58cbebf26506e49))
* Refine 'Use Repository Root' toggle: add cache invalidation and safety checks ([#312](https://github.com/hatayama/uLoopMCP/issues/312)) ([9fe056c](https://github.com/hatayama/uLoopMCP/commit/9fe056c3d893719ca071cff9eaffe347268f8b38))
* remove deprecated security settings section and fix typos ([#209](https://github.com/hatayama/uLoopMCP/issues/209)) ([05b91c3](https://github.com/hatayama/uLoopMCP/commit/05b91c3ac1d45caa4de67b7a02770cb627a9f66f))
* remove empty development mode conditionals and unused callbacks ([#271](https://github.com/hatayama/uLoopMCP/issues/271)) ([85f7e93](https://github.com/hatayama/uLoopMCP/commit/85f7e93790b7e5ffde6c4fca8807dcdf3e810d10))
* Remove package.json from bundle, use version.ts instead ([#368](https://github.com/hatayama/uLoopMCP/issues/368)) ([9140378](https://github.com/hatayama/uLoopMCP/commit/91403785da4e1ee92ffb857b5b251d24caf5683a))
* remove ping-based health checks to prevent false positives during Domain Reload ([#410](https://github.com/hatayama/uLoopMCP/issues/410)) ([4ec243e](https://github.com/hatayama/uLoopMCP/commit/4ec243e7821693128c9bb43c439f5a834c770666))
* remove Schedule-based polling from console utilities ([#284](https://github.com/hatayama/uLoopMCP/issues/284)) ([7c46c3f](https://github.com/hatayama/uLoopMCP/commit/7c46c3fe58837735b5890599060bc542effdff67))
* Remove unnecessary .meta ([#408](https://github.com/hatayama/uLoopMCP/issues/408)) ([caa47f0](https://github.com/hatayama/uLoopMCP/commit/caa47f01cd6806a360bf8dca447f56b3cf7725a1))
* remove unnecessary timing properties from BaseToolResponse ([#274](https://github.com/hatayama/uLoopMCP/issues/274)) ([2e319cc](https://github.com/hatayama/uLoopMCP/commit/2e319cccbe950f8a4ec0de9a946c44c1efa980e0))
* Rename capture-window MCP tool to screenshot ([#701](https://github.com/hatayama/uLoopMCP/issues/701)) ([b178bc8](https://github.com/hatayama/uLoopMCP/commit/b178bc899a87c9ad4fce86adc27611c56a9f2811))
* Rename settings.security.json to settings.permissions.json ([#713](https://github.com/hatayama/uLoopMCP/issues/713)) ([ae1a21a](https://github.com/hatayama/uLoopMCP/commit/ae1a21af2a575a94fd96476feb2fe05bfbb77f88))
* require manual server start on first launch ([#547](https://github.com/hatayama/uLoopMCP/issues/547)) ([fb29ddb](https://github.com/hatayama/uLoopMCP/commit/fb29ddb85abf9f1159e5733489e9de20effe66c3))
* resolve Cursor MCP server path relative to configuration root ([#327](https://github.com/hatayama/uLoopMCP/issues/327)) ([95bda03](https://github.com/hatayama/uLoopMCP/commit/95bda03baa83774220efe1801b3813850d2a0b6d))
* resolve ESLint security warnings and TypeScript compilation errors ([#224](https://github.com/hatayama/uLoopMCP/issues/224)) ([737c313](https://github.com/hatayama/uLoopMCP/commit/737c313a4e50d837b809144f94d6d3ef11ca90bc))
* resolve MCP inspector unknown request ID errors ([#215](https://github.com/hatayama/uLoopMCP/issues/215)) ([3418d62](https://github.com/hatayama/uLoopMCP/commit/3418d62c5c4b4efe90d5e079f412b1310e80e0ae))
* resolve TypeScript type errors in service registration and dependencies ([#250](https://github.com/hatayama/uLoopMCP/issues/250)) ([63a685d](https://github.com/hatayama/uLoopMCP/commit/63a685d6ec71a647ea812e4d77bc784e0e58b384))
* restructure READMEs to CLI-first layout with streamlined content ([#615](https://github.com/hatayama/uLoopMCP/issues/615)) ([05788e9](https://github.com/hatayama/uLoopMCP/commit/05788e98d1a73182dd9d6b71828d2f45c6ef9656))
* revert Cursor MCP server path change from [#327](https://github.com/hatayama/uLoopMCP/issues/327) ([#336](https://github.com/hatayama/uLoopMCP/issues/336)) ([a01515a](https://github.com/hatayama/uLoopMCP/commit/a01515a63492fb77fee0d210e98b3f0d83fbb59b))
* **server:** enable auto-start without opening EditorWindow ([#500](https://github.com/hatayama/uLoopMCP/issues/500)) ([5766bdb](https://github.com/hatayama/uLoopMCP/commit/5766bdba683fec2f1fe385c0e8d747e5e3e1d066))
* simplify timeout error message by removing verbose AI instructions ([#670](https://github.com/hatayama/uLoopMCP/issues/670)) ([97780a4](https://github.com/hatayama/uLoopMCP/commit/97780a4489ad7fcbbd191f7d18aec28758136260))
* Skip auto-sync for version/help flags and update command ([#624](https://github.com/hatayama/uLoopMCP/issues/624)) ([43db6e5](https://github.com/hatayama/uLoopMCP/commit/43db6e5d524673ae030cc4136a1e592b0cd306ea))
* Skip auto-sync when running launch command ([#621](https://github.com/hatayama/uLoopMCP/issues/621)) ([b3e4ee8](https://github.com/hatayama/uLoopMCP/commit/b3e4ee88d158f0b4716153a3b32dc2dd0c5cac75))
* stabilize Tool Settings toggle interaction during UI refresh ([#702](https://github.com/hatayama/uLoopMCP/issues/702)) ([6b4ba25](https://github.com/hatayama/uLoopMCP/commit/6b4ba25d2d7821abecf0a990c7021a9c5de64518))
* Standardize SKILL.md descriptions and reduce verbosity ([#733](https://github.com/hatayama/uLoopMCP/issues/733)) ([a743607](https://github.com/hatayama/uLoopMCP/commit/a743607a9bfa6ecdf5c31946f3776f662f4e0e15))
* support Node.js detection for nvm and other version managers ([#398](https://github.com/hatayama/uLoopMCP/issues/398)) ([96c71c9](https://github.com/hatayama/uLoopMCP/commit/96c71c971ee1fa1137cedf7dc9b7cf34ea2020fb))
* support Unity 6.3+ reference assembly path structure ([#378](https://github.com/hatayama/uLoopMCP/issues/378)) ([6d2ad25](https://github.com/hatayama/uLoopMCP/commit/6d2ad25255366df17e61b9ec09e6b6dfefb8527f))
* suppress unnecessary 'Multiple Unity projects' warning for non-project commands ([#671](https://github.com/hatayama/uLoopMCP/issues/671)) ([36bc1ca](https://github.com/hatayama/uLoopMCP/commit/36bc1cae5b5a9b59c266f4e76ef5011c5dbd5983))
* Sync lint-staged version in package.json with package-lock.json ([#735](https://github.com/hatayama/uLoopMCP/issues/735)) ([60eff43](https://github.com/hatayama/uLoopMCP/commit/60eff43cf8762f1075b3cb8e214d483aaf55af6e))
* trigger release ([#276](https://github.com/hatayama/uLoopMCP/issues/276)) ([2ae6a2d](https://github.com/hatayama/uLoopMCP/commit/2ae6a2da4cb8372cd4610f34e8163ad8f2b64b6a))
* **ui:** replace hardcoded package paths with dynamic resolution ([#516](https://github.com/hatayama/uLoopMCP/issues/516)) ([18becbc](https://github.com/hatayama/uLoopMCP/commit/18becbc7221c87dbb2da0ae49b11a5cf21f8a86c))
* unify MCP startup recovery to reuse original port after crash ([#318](https://github.com/hatayama/uLoopMCP/issues/318)) ([25535fb](https://github.com/hatayama/uLoopMCP/commit/25535fbc4a1c72577c172ccc633c47eb9b2312a3))
* Update hono and @hono/node-server to resolve high severity vulnerabilities ([#731](https://github.com/hatayama/uLoopMCP/issues/731)) ([135044a](https://github.com/hatayama/uLoopMCP/commit/135044ae761ef50144fb3a23c5118d6e53367ffe))
* update qs to 6.14.1 to fix DoS vulnerability ([#501](https://github.com/hatayama/uLoopMCP/issues/501)) ([04a9adc](https://github.com/hatayama/uLoopMCP/commit/04a9adca6b92893f795b0ac23c9ca398ead680cd))
* Update README docs and enable auto-start server by default ([#333](https://github.com/hatayama/uLoopMCP/issues/333)) ([d688872](https://github.com/hatayama/uLoopMCP/commit/d688872de1e0ea14b503c187add63ce6a9f4fa2b))
* update READMEs with --project-path option and comprehensive CLI reference ([#687](https://github.com/hatayama/uLoopMCP/issues/687)) ([7829c9d](https://github.com/hatayama/uLoopMCP/commit/7829c9dc6e21bc117b353251b71eab7bbe95b942))
* update skill documentation for new Skill folder structure ([#539](https://github.com/hatayama/uLoopMCP/issues/539)) ([45f1bd4](https://github.com/hatayama/uLoopMCP/commit/45f1bd4cd007b9457fb6eab3c9f319698ce16aea))
* use generic type for TypeScript package.json version sync ([#420](https://github.com/hatayama/uLoopMCP/issues/420)) ([786b57c](https://github.com/hatayama/uLoopMCP/commit/786b57c86007d5be2b274310cd1f1f0fb65d1192))
* use interactive login shell for executable detection to match terminal environment ([#667](https://github.com/hatayama/uLoopMCP/issues/667)) ([13ac67a](https://github.com/hatayama/uLoopMCP/commit/13ac67a001cf57454ac95c363fa0c07361d8288e))
* use portable "node" command instead of full path in mcp.json ([#399](https://github.com/hatayama/uLoopMCP/issues/399)) ([f3d9cb7](https://github.com/hatayama/uLoopMCP/commit/f3d9cb7e459f0561440aff522a0e1d9a2991eea5))
* wait for initialization before returning tools in list_tools handler ([#403](https://github.com/hatayama/uLoopMCP/issues/403)) ([4d15b0e](https://github.com/hatayama/uLoopMCP/commit/4d15b0e8d9fae89ab3c9155a41b25253f184a0b9))


### Miscellaneous Chores

* force release 0.45.2 ([b6f7596](https://github.com/hatayama/uLoopMCP/commit/b6f75966841ba77902ee65c87fa4cc68efe1821b))

## [0.70.1](https://github.com/hatayama/uLoopMCP/compare/v0.70.0...v0.70.1) (2026-03-15)


### Bug Fixes

* Classify csc.rsp compiler diagnostics correctly in get-logs LogType filter ([#761](https://github.com/hatayama/uLoopMCP/issues/761)) ([#767](https://github.com/hatayama/uLoopMCP/issues/767)) ([7069c5f](https://github.com/hatayama/uLoopMCP/commit/7069c5f5a0b7d646060200373515d65fa8d24809))

## [0.70.0](https://github.com/hatayama/uLoopMCP/compare/v0.69.6...v0.70.0) (2026-03-15)


### Features

* Add simulate-mouse tool for PlayMode UI interaction ([#759](https://github.com/hatayama/uLoopMCP/issues/759)) ([5679f34](https://github.com/hatayama/uLoopMCP/commit/5679f342932a67aecefbff804bd7265fdd6ec00d))

## [0.69.6](https://github.com/hatayama/uLoopMCP/compare/v0.69.5...v0.69.6) (2026-03-06)


### Bug Fixes

* apply context: fork to uloop-execute-dynamic-code skill and add wiring references ([#743](https://github.com/hatayama/uLoopMCP/issues/743)) ([4ab452b](https://github.com/hatayama/uLoopMCP/commit/4ab452ba9efde76704b18d58d403761df2128941))

## [0.69.5](https://github.com/hatayama/uLoopMCP/compare/v0.69.4...v0.69.5) (2026-03-06)


### Bug Fixes

* isolate Roslyn dependencies via shared registry ([#741](https://github.com/hatayama/uLoopMCP/issues/741)) ([c96a3a2](https://github.com/hatayama/uLoopMCP/commit/c96a3a2e06a268cc4daa5cb0eb9936b638abf5bb))

## [0.69.4](https://github.com/hatayama/uLoopMCP/compare/v0.69.3...v0.69.4) (2026-03-05)


### Bug Fixes

* Standardize SKILL.md descriptions and reduce verbosity ([#733](https://github.com/hatayama/uLoopMCP/issues/733)) ([a743607](https://github.com/hatayama/uLoopMCP/commit/a743607a9bfa6ecdf5c31946f3776f662f4e0e15))
* Sync lint-staged version in package.json with package-lock.json ([#735](https://github.com/hatayama/uLoopMCP/issues/735)) ([60eff43](https://github.com/hatayama/uLoopMCP/commit/60eff43cf8762f1075b3cb8e214d483aaf55af6e))

## [0.69.3](https://github.com/hatayama/uLoopMCP/compare/v0.69.2...v0.69.3) (2026-03-05)


### Bug Fixes

* Handle Win32Exception in skills install process start ([#730](https://github.com/hatayama/uLoopMCP/issues/730)) ([cd7a4a3](https://github.com/hatayama/uLoopMCP/commit/cd7a4a3a19ab1d4830d1131074caeab4e85969e1))
* Update hono and @hono/node-server to resolve high severity vulnerabilities ([#731](https://github.com/hatayama/uLoopMCP/issues/731)) ([135044a](https://github.com/hatayama/uLoopMCP/commit/135044ae761ef50144fb3a23c5118d6e53367ffe))

## [0.69.2](https://github.com/hatayama/uLoopMCP/compare/v0.69.1...v0.69.2) (2026-03-03)


### Bug Fixes

* Prevent rename migration from shadowing legacy settings migration ([#725](https://github.com/hatayama/uLoopMCP/issues/725)) ([b03337b](https://github.com/hatayama/uLoopMCP/commit/b03337be8892cc807e8f6a47beab023c7d985bc9))

## [0.69.1](https://github.com/hatayama/uLoopMCP/compare/v0.69.0...v0.69.1) (2026-03-03)


### Bug Fixes

* Add GetVersionTool to core package and improve CLI error handling ([#723](https://github.com/hatayama/uLoopMCP/issues/723)) ([21afcc8](https://github.com/hatayama/uLoopMCP/commit/21afcc899d7b8857737a7bda9480ccb447f9083c))

## [0.69.0](https://github.com/hatayama/uLoopMCP/compare/v0.68.3...v0.69.0) (2026-03-03)


### Features

* Add delete button for MCP configuration ([#718](https://github.com/hatayama/uLoopMCP/issues/718)) ([3e4c4fb](https://github.com/hatayama/uLoopMCP/commit/3e4c4fb907b1fc446e19f3eba98bf6afc174a3ee))


### Bug Fixes

* Prevent CLI from sending commands to wrong Unity instance ([#719](https://github.com/hatayama/uLoopMCP/issues/719)) ([d0e47b3](https://github.com/hatayama/uLoopMCP/commit/d0e47b392677af36dfba740e9c7e80579877bad8))
* Prevent toggle ChangeEvent from collapsing parent Foldout ([#721](https://github.com/hatayama/uLoopMCP/issues/721)) ([f6caf9b](https://github.com/hatayama/uLoopMCP/commit/f6caf9b7066f5f4587788c7e3b2d9e8ce0062aa3))
* Rename settings.security.json to settings.permissions.json ([#713](https://github.com/hatayama/uLoopMCP/issues/713)) ([ae1a21a](https://github.com/hatayama/uLoopMCP/commit/ae1a21af2a575a94fd96476feb2fe05bfbb77f88))

## [0.68.3](https://github.com/hatayama/uLoopMCP/compare/v0.68.2...v0.68.3) (2026-03-02)


### Bug Fixes

* Group help commands by category in uloop -h ([#708](https://github.com/hatayama/uLoopMCP/issues/708)) ([dbfe539](https://github.com/hatayama/uLoopMCP/commit/dbfe5394172a80f550304b95787d36d4046d0404))

## [0.68.2](https://github.com/hatayama/uLoopMCP/compare/v0.68.1...v0.68.2) (2026-03-01)


### Bug Fixes

* Hide disabled tools from CLI help and shell completion output ([#705](https://github.com/hatayama/uLoopMCP/issues/705)) ([3f49750](https://github.com/hatayama/uLoopMCP/commit/3f4975078e324b56e4363c510c6081b7373afa63))

## [0.68.1](https://github.com/hatayama/uLoopMCP/compare/v0.68.0...v0.68.1) (2026-03-01)


### Bug Fixes

* stabilize Tool Settings toggle interaction during UI refresh ([#702](https://github.com/hatayama/uLoopMCP/issues/702)) ([6b4ba25](https://github.com/hatayama/uLoopMCP/commit/6b4ba25d2d7821abecf0a990c7021a9c5de64518))

## [0.68.0](https://github.com/hatayama/uLoopMCP/compare/v0.67.5...v0.68.0) (2026-02-28)


### Features

* Add per-tool enable/disable toggle for MCP tools ([#698](https://github.com/hatayama/uLoopMCP/issues/698)) ([5ca8b4c](https://github.com/hatayama/uLoopMCP/commit/5ca8b4ca99ed618148a93d74a9dd76fb6f0cff60))
* Migrate security settings to project-scoped .uloop/settings.security.json ([#696](https://github.com/hatayama/uLoopMCP/issues/696)) ([222d46e](https://github.com/hatayama/uLoopMCP/commit/222d46e485b5b0006f8ed9e74e19e191938f2010))


### Bug Fixes

* Improve tool settings UI message to mention context window benefit ([#700](https://github.com/hatayama/uLoopMCP/issues/700)) ([040c8bd](https://github.com/hatayama/uLoopMCP/commit/040c8bd8687d8925219285f3b401c3d6900a5f3e))
* Rename capture-window MCP tool to screenshot ([#701](https://github.com/hatayama/uLoopMCP/issues/701)) ([b178bc8](https://github.com/hatayama/uLoopMCP/commit/b178bc899a87c9ad4fce86adc27611c56a9f2811))

## [0.67.5](https://github.com/hatayama/uLoopMCP/compare/v0.67.4...v0.67.5) (2026-02-26)


### Bug Fixes

* add meta ([#692](https://github.com/hatayama/uLoopMCP/issues/692)) ([34b29de](https://github.com/hatayama/uLoopMCP/commit/34b29de1ea497e8755f6c8cf711eebf7b1378f15))

## [0.67.4](https://github.com/hatayama/uLoopMCP/compare/v0.67.3...v0.67.4) (2026-02-26)


### Bug Fixes

* correct SKILL.md parameter docs to match C# implementations ([#689](https://github.com/hatayama/uLoopMCP/issues/689)) ([e76ab29](https://github.com/hatayama/uLoopMCP/commit/e76ab29372c28f6513016d11b9fb7cb18e6f025f))
* Fix incomplete property serialization in ComponentPropertySerializer ([#691](https://github.com/hatayama/uLoopMCP/issues/691)) ([2db6eb3](https://github.com/hatayama/uLoopMCP/commit/2db6eb3f63865ab6e56c41cabbbe3a37d183b164))

## [0.67.3](https://github.com/hatayama/uLoopMCP/compare/v0.67.2...v0.67.3) (2026-02-26)


### Bug Fixes

* update READMEs with --project-path option and comprehensive CLI reference ([#687](https://github.com/hatayama/uLoopMCP/issues/687)) ([7829c9d](https://github.com/hatayama/uLoopMCP/commit/7829c9dc6e21bc117b353251b71eab7bbe95b942))

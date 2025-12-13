# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.43.9](https://github.com/hatayama/uLoopMCP/compare/v0.43.8...v0.43.9) (2025-12-13)


### Bug Fixes

* fix Unity window focus functionality and add test script ([#412](https://github.com/hatayama/uLoopMCP/issues/412)) ([b9293e9](https://github.com/hatayama/uLoopMCP/commit/b9293e905343a4ee66ec0332cdf08be16b309121))

## [0.43.8](https://github.com/hatayama/uLoopMCP/compare/v0.43.7...v0.43.8) (2025-12-13)


### Bug Fixes

* remove ping-based health checks to prevent false positives during Domain Reload ([#410](https://github.com/hatayama/uLoopMCP/issues/410)) ([4ec243e](https://github.com/hatayama/uLoopMCP/commit/4ec243e7821693128c9bb43c439f5a834c770666))

## [0.43.7](https://github.com/hatayama/uLoopMCP/compare/v0.43.6...v0.43.7) (2025-12-13)


### Bug Fixes

* Remove unnecessary .meta ([#408](https://github.com/hatayama/uLoopMCP/issues/408)) ([caa47f0](https://github.com/hatayama/uLoopMCP/commit/caa47f01cd6806a360bf8dca447f56b3cf7725a1))

## [0.43.6](https://github.com/hatayama/uLoopMCP/compare/v0.43.5...v0.43.6) (2025-12-12)


### Bug Fixes

* clear pending requests on socket close and reduce timeout to 3 minutes ([#405](https://github.com/hatayama/uLoopMCP/issues/405)) ([7dbc7e6](https://github.com/hatayama/uLoopMCP/commit/7dbc7e6a207c486f44e0397593be977d3f556d11))

## [0.43.5](https://github.com/hatayama/uLoopMCP/compare/v0.43.4...v0.43.5) (2025-12-11)


### Bug Fixes

* wait for initialization before returning tools in list_tools handler ([#403](https://github.com/hatayama/uLoopMCP/issues/403)) ([4d15b0e](https://github.com/hatayama/uLoopMCP/commit/4d15b0e8d9fae89ab3c9155a41b25253f184a0b9))

## [0.43.4](https://github.com/hatayama/uLoopMCP/compare/v0.43.3...v0.43.4) (2025-12-11)


### Bug Fixes

* prevent timeout from affecting unrelated pending requests ([#401](https://github.com/hatayama/uLoopMCP/issues/401)) ([2a533fc](https://github.com/hatayama/uLoopMCP/commit/2a533fc8627d523b3c49ac133d2761aeb9219cf0))

## [0.43.3](https://github.com/hatayama/uLoopMCP/compare/v0.43.2...v0.43.3) (2025-12-11)


### Bug Fixes

* use portable "node" command instead of full path in mcp.json ([#399](https://github.com/hatayama/uLoopMCP/issues/399)) ([f3d9cb7](https://github.com/hatayama/uLoopMCP/commit/f3d9cb7e459f0561440aff522a0e1d9a2991eea5))

## [0.43.2](https://github.com/hatayama/uLoopMCP/compare/v0.43.1...v0.43.2) (2025-12-11)


### Bug Fixes

* detect Unity project root dynamically for VibeLogger ([dd5568c](https://github.com/hatayama/uLoopMCP/commit/dd5568c9189f47d1d1b67d668867ea6095fa154e))
* support Node.js detection for nvm and other version managers ([#398](https://github.com/hatayama/uLoopMCP/issues/398)) ([96c71c9](https://github.com/hatayama/uLoopMCP/commit/96c71c971ee1fa1137cedf7dc9b7cf34ea2020fb))

## [0.43.1](https://github.com/hatayama/uLoopMCP/compare/v0.43.0...v0.43.1) (2025-12-11)


### Bug Fixes

* increase network timeout to 5min and improve timeout error message ([#393](https://github.com/hatayama/uLoopMCP/issues/393)) ([cc95840](https://github.com/hatayama/uLoopMCP/commit/cc9584093f8ee3aee93bbe34437fc2189d0363eb))

## [0.43.0](https://github.com/hatayama/uLoopMCP/compare/v0.42.1...v0.43.0) (2025-12-10)


### Features

* prevent Cursor MCP disconnection on package updates with stable server path ([#386](https://github.com/hatayama/uLoopMCP/issues/386)) ([171a1a9](https://github.com/hatayama/uLoopMCP/commit/171a1a9d836302b649dd9c7a9e5cfac48f58ce61))
* return success message for pending requests on temporary disconnect ([#392](https://github.com/hatayama/uLoopMCP/issues/392)) ([b2bfa6d](https://github.com/hatayama/uLoopMCP/commit/b2bfa6d497a86f9809e88ff70282de640f1ac4c8))
* skip server auto-start on first launch ([#390](https://github.com/hatayama/uLoopMCP/issues/390)) ([3b320eb](https://github.com/hatayama/uLoopMCP/commit/3b320eb8cad2c540e50f041a846431714ea03be9))
* verify Node.js availability before MCP configuration ([#388](https://github.com/hatayama/uLoopMCP/issues/388)) ([3bf2c29](https://github.com/hatayama/uLoopMCP/commit/3bf2c29208bac74820ce48bc61df4b7cd08a5181))


### Bug Fixes

* recover from stuck connection state after long-term operation ([#389](https://github.com/hatayama/uLoopMCP/issues/389)) ([1244a5d](https://github.com/hatayama/uLoopMCP/commit/1244a5d7d108b08d0e78c210e58cbebf26506e49))

## [0.42.1](https://github.com/hatayama/uLoopMCP/compare/v0.42.0...v0.42.1) (2025-12-09)


### Bug Fixes

* prevent port conflict dialog during domain reload recovery ([#383](https://github.com/hatayama/uLoopMCP/issues/383)) ([0456c75](https://github.com/hatayama/uLoopMCP/commit/0456c7535d9df290207ca64e7c0ab725f4aceeee))

## [0.42.0](https://github.com/hatayama/uLoopMCP/compare/v0.41.4...v0.42.0) (2025-12-09)


### Features

* Add MCP keepalive service to prevent Cursor idle timeout ([#381](https://github.com/hatayama/uLoopMCP/issues/381)) ([cbdce47](https://github.com/hatayama/uLoopMCP/commit/cbdce4732cf36a01384df4eea362545f693822bf))

## [0.41.4](https://github.com/hatayama/uLoopMCP/compare/v0.41.3...v0.41.4) (2025-12-09)


### Bug Fixes

* support Unity 6.3+ reference assembly path structure ([#378](https://github.com/hatayama/uLoopMCP/issues/378)) ([6d2ad25](https://github.com/hatayama/uLoopMCP/commit/6d2ad25255366df17e61b9ec09e6b6dfefb8527f))

## [0.41.3](https://github.com/hatayama/uLoopMCP/compare/v0.41.2...v0.41.3) (2025-12-09)


### Bug Fixes

* prevent Cursor MCP disconnection by fixing initialization race conditions ([#376](https://github.com/hatayama/uLoopMCP/issues/376)) ([220e553](https://github.com/hatayama/uLoopMCP/commit/220e5532561fc9ac0b6e5d843af5c08da8d305b3))

## [0.41.2](https://github.com/hatayama/uLoopMCP/compare/v0.41.1...v0.41.2) (2025-12-03)


### Bug Fixes

* Enhance supply chain security and dependency management ([#361](https://github.com/hatayama/uLoopMCP/issues/361)) ([1cb2165](https://github.com/hatayama/uLoopMCP/commit/1cb2165abbb9f118801cac8165fce615e8e8ba83))
* Remove package.json from bundle, use version.ts instead ([#368](https://github.com/hatayama/uLoopMCP/issues/368)) ([9140378](https://github.com/hatayama/uLoopMCP/commit/91403785da4e1ee92ffb857b5b251d24caf5683a))

## [0.41.1](https://github.com/hatayama/uLoopMCP/compare/v0.41.0...v0.41.1) (2025-12-01)


### Bug Fixes

* improve disconnected messages based on Unity shutdown reason ([#353](https://github.com/hatayama/uLoopMCP/issues/353)) ([d23034e](https://github.com/hatayama/uLoopMCP/commit/d23034e7eb19b516af54123f846d8739c6856ff1))

## [0.41.0](https://github.com/hatayama/uLoopMCP/compare/v0.40.2...v0.41.0) (2025-11-29)


### Features

* Add MCP configuration auto-update utility before server startup ([#349](https://github.com/hatayama/uLoopMCP/issues/349)) ([1d510d1](https://github.com/hatayama/uLoopMCP/commit/1d510d1d6648a4430414041cddc8326626c7112e))


### Bug Fixes

* Auto Configuration File Path Updates During Auto Start Server Recovery ([#351](https://github.com/hatayama/uLoopMCP/issues/351)) ([013478f](https://github.com/hatayama/uLoopMCP/commit/013478f210550c69d25589a3531c08f805440d22))

## [0.40.2](https://github.com/hatayama/uLoopMCP/compare/v0.40.1...v0.40.2) (2025-11-28)


### Bug Fixes

* Add Directory.Exists check before creating config directory ([#345](https://github.com/hatayama/uLoopMCP/issues/345)) ([ac14c61](https://github.com/hatayama/uLoopMCP/commit/ac14c61f4da976398e9855b398f8eabb57534bfa))

## [0.40.1](https://github.com/hatayama/uLoopMCP/compare/v0.40.0...v0.40.1) (2025-11-28)


### Bug Fixes

* Improve temporary disconnection handling for better LLM interaction ([#343](https://github.com/hatayama/uLoopMCP/issues/343)) ([80dc21a](https://github.com/hatayama/uLoopMCP/commit/80dc21aa235517850387b4572bef45f3f27056e6))

## [0.40.0](https://github.com/hatayama/uLoopMCP/compare/v0.39.0...v0.40.0) (2025-11-24)


### Features

* Add Windows support for focus-window tool ([#338](https://github.com/hatayama/uLoopMCP/issues/338)) ([101267d](https://github.com/hatayama/uLoopMCP/commit/101267dcb594e8324bdea3c88c2163a9e5627561))


### Bug Fixes

* Improve error handling and simplify tool response messages ([#340](https://github.com/hatayama/uLoopMCP/issues/340)) ([a3c6dd2](https://github.com/hatayama/uLoopMCP/commit/a3c6dd2740bfd07c2b0a8adfbc0f4467de02374e))

## [0.39.0](https://github.com/hatayama/uLoopMCP/compare/v0.38.0...v0.39.0) (2025-11-23)


### Features

* add macOS focus-window MCP tool ([#337](https://github.com/hatayama/uLoopMCP/issues/337)) ([0fc217a](https://github.com/hatayama/uLoopMCP/commit/0fc217af20447b1798c12ff4d2a55c6ebe8df016))


### Bug Fixes

* keep TCP port stable after crash recovery ([#335](https://github.com/hatayama/uLoopMCP/issues/335)) ([e2df9cf](https://github.com/hatayama/uLoopMCP/commit/e2df9cf348e9a8e1eba33e18654575740b120a94))
* revert Cursor MCP server path change from [#327](https://github.com/hatayama/uLoopMCP/issues/327) ([#336](https://github.com/hatayama/uLoopMCP/issues/336)) ([a01515a](https://github.com/hatayama/uLoopMCP/commit/a01515a63492fb77fee0d210e98b3f0d83fbb59b))
* Update README docs and enable auto-start server by default ([#333](https://github.com/hatayama/uLoopMCP/issues/333)) ([d688872](https://github.com/hatayama/uLoopMCP/commit/d688872de1e0ea14b503c187add63ce6a9f4fa2b))

## [0.38.0](https://github.com/hatayama/uLoopMCP/compare/v0.37.2...v0.38.0) (2025-11-20)


### Features

* Enable Codex configuration on Windows ([#331](https://github.com/hatayama/uLoopMCP/issues/331)) ([1ece6d7](https://github.com/hatayama/uLoopMCP/commit/1ece6d7f1035a16d8df7ba9dc147a87085c664b6))

## [0.37.2](https://github.com/hatayama/uLoopMCP/compare/v0.37.1...v0.37.2) (2025-11-19)


### Bug Fixes

* resolve Cursor MCP server path relative to configuration root ([#327](https://github.com/hatayama/uLoopMCP/issues/327)) ([95bda03](https://github.com/hatayama/uLoopMCP/commit/95bda03baa83774220efe1801b3813850d2a0b6d))

## [0.37.1](https://github.com/hatayama/uLoopMCP/compare/v0.37.0...v0.37.1) (2025-10-28)


### Bug Fixes

* unify MCP startup recovery to reuse original port after crash ([#318](https://github.com/hatayama/uLoopMCP/issues/318)) ([25535fb](https://github.com/hatayama/uLoopMCP/commit/25535fbc4a1c72577c172ccc633c47eb9b2312a3))

## [0.37.0](https://github.com/hatayama/uLoopMCP/compare/v0.36.0...v0.37.0) (2025-10-27)


### Features

* async-aware execute-dynamic-code pipeline ([#316](https://github.com/hatayama/uLoopMCP/issues/316)) ([2c699a9](https://github.com/hatayama/uLoopMCP/commit/2c699a93634e6f0371586bfb1408b7036c56191e))

## [0.36.0](https://github.com/hatayama/uLoopMCP/compare/v0.35.1...v0.36.0) (2025-10-27)


### Features

* improve hierarchy export and RootPath matching ([#314](https://github.com/hatayama/uLoopMCP/issues/314)) ([8be0e18](https://github.com/hatayama/uLoopMCP/commit/8be0e189ae6b60d78f97c3369ca6adf988b33efe))

## [0.35.1](https://github.com/hatayama/uLoopMCP/compare/v0.35.0...v0.35.1) (2025-10-22)


### Bug Fixes

* Refine 'Use Repository Root' toggle: add cache invalidation and safety checks ([#312](https://github.com/hatayama/uLoopMCP/issues/312)) ([9fe056c](https://github.com/hatayama/uLoopMCP/commit/9fe056c3d893719ca071cff9eaffe347268f8b38))

## [0.35.0](https://github.com/hatayama/uLoopMCP/compare/v0.34.2...v0.35.0) (2025-10-22)


### Features

* Add 'Use Repository Root' toggle for monorepo support ([#308](https://github.com/hatayama/uLoopMCP/issues/308)) ([c958339](https://github.com/hatayama/uLoopMCP/commit/c95833943e454e186eca99d28752f06640436a21))

## [0.34.2](https://github.com/hatayama/uLoopMCP/compare/v0.34.1...v0.34.2) (2025-10-21)


### Bug Fixes

* Ensure Unity client retries after initial socket failure ([#306](https://github.com/hatayama/uLoopMCP/issues/306)) ([04cba58](https://github.com/hatayama/uLoopMCP/commit/04cba5887de3647d9622a14136abc2ae33e90fbc))

## [0.34.1](https://github.com/hatayama/uLoopMCP/compare/v0.34.0...v0.34.1) (2025-10-15)


### Bug Fixes

* Improve connection error message with post-compile guidance ([#304](https://github.com/hatayama/uLoopMCP/issues/304)) ([c04921d](https://github.com/hatayama/uLoopMCP/commit/c04921d2655435283ba89f03d008b4d7c91fbeed))

## [0.34.0](https://github.com/hatayama/uLoopMCP/compare/v0.33.0...v0.34.0) (2025-10-01)


### Features

* trigger minor release ([#302](https://github.com/hatayama/uLoopMCP/issues/302)) ([b652ac1](https://github.com/hatayama/uLoopMCP/commit/b652ac174c8662bb73395ec3f43f1e22e63cc6dd))

## [0.33.0](https://github.com/hatayama/uLoopMCP/compare/v0.32.2...v0.33.0) (2025-09-16)


### Features

* **Editor:** add Codex MCP TOML config service and update Editor UI ([#292](https://github.com/hatayama/uLoopMCP/issues/292)) ([612fc70](https://github.com/hatayama/uLoopMCP/commit/612fc7032d39339557cc6db2c88469533db813a3))

## [0.32.2](https://github.com/hatayama/uLoopMCP/compare/v0.32.1...v0.32.2) (2025-09-10)


### Bug Fixes

* make mcp.json equality check order-insensitive to avoid needless rewrites ([#293](https://github.com/hatayama/uLoopMCP/issues/293)) ([9e8444d](https://github.com/hatayama/uLoopMCP/commit/9e8444d5bcf964b160ef2a6ab40dacecbc01b613))

## [0.32.1](https://github.com/hatayama/uLoopMCP/compare/v0.32.0...v0.32.1) (2025-09-02)


### Bug Fixes

* (execute-dynamic-code)  return optional, better Parameters guidance, and test ([#289](https://github.com/hatayama/uLoopMCP/issues/289)) ([3d8f954](https://github.com/hatayama/uLoopMCP/commit/3d8f954a5a9d2374b79635706bc15b6c2b512221))

## [0.32.0](https://github.com/hatayama/uLoopMCP/compare/v0.31.1...v0.32.0) (2025-09-01)


### Features

* Add dynamic code execution with multi-level security framework ([#286](https://github.com/hatayama/uLoopMCP/issues/286)) ([8ad24de](https://github.com/hatayama/uLoopMCP/commit/8ad24de34a573909ecbb2e0fad25e8cd7fa62837))

## [0.31.1](https://github.com/hatayama/uLoopMCP/compare/v0.31.0...v0.31.1) (2025-08-18)


### Bug Fixes

* remove Schedule-based polling from console utilities ([#284](https://github.com/hatayama/uLoopMCP/issues/284)) ([7c46c3f](https://github.com/hatayama/uLoopMCP/commit/7c46c3fe58837735b5890599060bc542effdff67))

## [0.31.0](https://github.com/hatayama/uLoopMCP/compare/v0.30.5...v0.31.0) (2025-08-06)


### Features

* add CaptureGameView MCP tool for Unity screenshot capture ([#281](https://github.com/hatayama/uLoopMCP/issues/281)) ([3db45f0](https://github.com/hatayama/uLoopMCP/commit/3db45f01df6ad535ca22a9d81d0e4cebaface9e6))


### Bug Fixes

* handle duplicate MenuItem attributes with improved warnings ([#283](https://github.com/hatayama/uLoopMCP/issues/283)) ([3c7b98a](https://github.com/hatayama/uLoopMCP/commit/3c7b98a0e7ffba33d9a9c06b7c7947c21fc941ee))

## [0.30.5](https://github.com/hatayama/uLoopMCP/compare/v0.30.4...v0.30.5) (2025-07-31)


### Bug Fixes

* trigger release ([#276](https://github.com/hatayama/uLoopMCP/issues/276)) ([2ae6a2d](https://github.com/hatayama/uLoopMCP/commit/2ae6a2da4cb8372cd4610f34e8163ad8f2b64b6a))

## [0.30.4](https://github.com/hatayama/uLoopMCP/compare/v0.30.3...v0.30.4) (2025-07-31)


### Bug Fixes

* remove empty development mode conditionals and unused callbacks ([#271](https://github.com/hatayama/uLoopMCP/issues/271)) ([85f7e93](https://github.com/hatayama/uLoopMCP/commit/85f7e93790b7e5ffde6c4fca8807dcdf3e810d10))
* remove unnecessary timing properties from BaseToolResponse ([#274](https://github.com/hatayama/uLoopMCP/issues/274)) ([2e319cc](https://github.com/hatayama/uLoopMCP/commit/2e319cccbe950f8a4ec0de9a946c44c1efa980e0))

## [0.30.3](https://github.com/hatayama/uLoopMCP/compare/v0.30.2...v0.30.3) (2025-07-27)


### Bug Fixes

* Enhance Connected Tools UI with Port Display and Better Contrast ([#268](https://github.com/hatayama/uLoopMCP/issues/268)) ([04589f6](https://github.com/hatayama/uLoopMCP/commit/04589f616a474dd4c448a9c24a9acd273d4568b7))

## [0.30.2](https://github.com/hatayama/uLoopMCP/compare/v0.30.1...v0.30.2) (2025-07-26)


### Bug Fixes

* resolve TypeScript type errors in service registration and dependencies ([#250](https://github.com/hatayama/uLoopMCP/issues/250)) ([63a685d](https://github.com/hatayama/uLoopMCP/commit/63a685d6ec71a647ea812e4d77bc784e0e58b384))

## [0.30.1](https://github.com/hatayama/uLoopMCP/compare/v0.30.0...v0.30.1) (2025-07-20)


### Bug Fixes

* improve UI responsiveness and port validation behavior ([#235](https://github.com/hatayama/uLoopMCP/issues/235)) ([6e11384](https://github.com/hatayama/uLoopMCP/commit/6e1138446f23660f02110b8a40d7b1c54bc651ca))

## [0.30.0](https://github.com/hatayama/uLoopMCP/compare/v0.29.4...v0.30.0) (2025-07-20)


### Features

* add automatic MCP configuration synchronization and enhanced button UI ([#231](https://github.com/hatayama/uLoopMCP/issues/231)) ([bfaf6df](https://github.com/hatayama/uLoopMCP/commit/bfaf6df76b72e6127a589658de36aaa65663f5e3))

## [0.29.4](https://github.com/hatayama/uLoopMCP/compare/v0.29.3...v0.29.4) (2025-07-20)


### Bug Fixes

* eliminate 'No connected tools found' flash with event-driven architecture ([#229](https://github.com/hatayama/uLoopMCP/issues/229)) ([8fc99f7](https://github.com/hatayama/uLoopMCP/commit/8fc99f73c760bd0d07c665d2b2bb79f60c84869e))

## [0.29.3](https://github.com/hatayama/uLoopMCP/compare/v0.29.2...v0.29.3) (2025-07-18)


### Bug Fixes

* resolve ESLint security warnings and TypeScript compilation errors ([#224](https://github.com/hatayama/uLoopMCP/issues/224)) ([737c313](https://github.com/hatayama/uLoopMCP/commit/737c313a4e50d837b809144f94d6d3ef11ca90bc))

## [0.29.2](https://github.com/hatayama/uLoopMCP/compare/v0.29.1...v0.29.2) (2025-07-17)


### Bug Fixes

* MCP client name display and Unity connection timeout issues ([#222](https://github.com/hatayama/uLoopMCP/issues/222)) ([e002763](https://github.com/hatayama/uLoopMCP/commit/e00276380dc534855296fa7a12590b8cecd6e730))

## [0.29.1](https://github.com/hatayama/uLoopMCP/compare/v0.29.0...v0.29.1) (2025-07-17)


### Bug Fixes

* domain reload connection stability and eliminate duplicate tool connections ([#220](https://github.com/hatayama/uLoopMCP/issues/220)) ([c983ed0](https://github.com/hatayama/uLoopMCP/commit/c983ed0fbbe24145e2834d98e6d2f4f6bb19fe3c))

## [0.29.0](https://github.com/hatayama/uLoopMCP/compare/v0.28.4...v0.29.0) (2025-07-17)


### Features

* restore and enhance security settings with third-party tool control ([#217](https://github.com/hatayama/uLoopMCP/issues/217)) ([144c04d](https://github.com/hatayama/uLoopMCP/commit/144c04ddc4275c7f6a4e2a3e52e02e54a2326c55))

## [0.28.4](https://github.com/hatayama/uLoopMCP/compare/v0.28.3...v0.28.4) (2025-07-17)


### Bug Fixes

* resolve MCP inspector unknown request ID errors ([#215](https://github.com/hatayama/uLoopMCP/issues/215)) ([3418d62](https://github.com/hatayama/uLoopMCP/commit/3418d62c5c4b4efe90d5e079f412b1310e80e0ae))

## [0.28.3](https://github.com/hatayama/uLoopMCP/compare/v0.28.2...v0.28.3) (2025-07-17)


### Bug Fixes

* Enforce mandatory UNITY_TCP_PORT environment variable validation ([#213](https://github.com/hatayama/uLoopMCP/issues/213)) ([415b834](https://github.com/hatayama/uLoopMCP/commit/415b8349518c60a7176fb4f603f8e17091d85872))

## [0.28.2](https://github.com/hatayama/uLoopMCP/compare/v0.28.1...v0.28.2) (2025-07-16)


### Bug Fixes

* improve reconnection speed after Unity domain reload ([#211](https://github.com/hatayama/uLoopMCP/issues/211)) ([b7e173c](https://github.com/hatayama/uLoopMCP/commit/b7e173c56bfa588471f0c3e6f7f6b59567cf06bf))

## [0.28.1](https://github.com/hatayama/uLoopMCP/compare/v0.28.0...v0.28.1) (2025-07-16)


### Bug Fixes

* remove deprecated security settings section and fix typos ([#209](https://github.com/hatayama/uLoopMCP/issues/209)) ([05b91c3](https://github.com/hatayama/uLoopMCP/commit/05b91c3ac1d45caa4de67b7a02770cb627a9f66f))

## [0.28.0](https://github.com/hatayama/uLoopMCP/compare/v0.27.1...v0.28.0) (2025-07-16)


### Features

* add regex and stack trace search support to get-logs tool ([#207](https://github.com/hatayama/uLoopMCP/issues/207)) ([745135a](https://github.com/hatayama/uLoopMCP/commit/745135a67f678734c911f07be73a486bc29324c2))

## [0.27.1](https://github.com/hatayama/uLoopMCP/compare/v0.27.0...v0.27.1) (2025-07-16)


### Bug Fixes

* Remove security settings and improve I/O error handling ([#205](https://github.com/hatayama/uLoopMCP/issues/205)) ([eb84a46](https://github.com/hatayama/uLoopMCP/commit/eb84a46eed8121609893b221ae3cb92147c3c558))

## [0.27.0](https://github.com/hatayama/uLoopMCP/compare/v0.26.5...v0.27.0) (2025-07-16)


### Features

* improve Content-Length framing with Buffer support and test reorganization ([#203](https://github.com/hatayama/uLoopMCP/issues/203)) ([63f4452](https://github.com/hatayama/uLoopMCP/commit/63f4452f4fa501c1a3f1c9fcb78dbc538fbed08f))

## [0.26.5](https://github.com/hatayama/uLoopMCP/compare/v0.26.4...v0.26.5) (2025-07-16)


### Bug Fixes

* move sample tools to dedicated Samples namespace ([#201](https://github.com/hatayama/uLoopMCP/issues/201)) ([51cfd7a](https://github.com/hatayama/uLoopMCP/commit/51cfd7a37295424b0d253fcb1dc89273dce5762e))

## [0.26.4](https://github.com/hatayama/uLoopMCP/compare/v0.26.3...v0.26.4) (2025-07-12)


### Bug Fixes

* consolidate feature documentation into collapsible README sections ([#196](https://github.com/hatayama/uLoopMCP/issues/196)) ([04f832a](https://github.com/hatayama/uLoopMCP/commit/04f832a7d1844b00a931a22b6f34d28fd0726b0a))

## [0.26.3](https://github.com/hatayama/uLoopMCP/compare/v0.26.2...v0.26.3) (2025-07-10)


### Bug Fixes

* replace JsonUtility with Newtonsoft.Json in file export ([#192](https://github.com/hatayama/uLoopMCP/issues/192)) ([8d8aa9f](https://github.com/hatayama/uLoopMCP/commit/8d8aa9fb852ed01f0c525d1e78bf1aeef951bb84))

## [0.26.2](https://github.com/hatayama/uLoopMCP/compare/v0.26.1...v0.26.2) (2025-07-10)


### Bug Fixes

* remove JSON depth limit and fix MaxResponseSizeKB threshold ([#190](https://github.com/hatayama/uLoopMCP/issues/190)) ([1825e73](https://github.com/hatayama/uLoopMCP/commit/1825e73e64b491dec8e2c42005d5eb794a889fd3))

## [0.26.1](https://github.com/hatayama/uLoopMCP/compare/v0.26.0...v0.26.1) (2025-07-10)


### Bug Fixes

* implement proper timeout handling with CancellationToken support ([#188](https://github.com/hatayama/uLoopMCP/issues/188)) ([3d6751e](https://github.com/hatayama/uLoopMCP/commit/3d6751e174fda1316c3dff2c40c3a49468acd86f))

## [0.26.0](https://github.com/hatayama/uLoopMCP/compare/v0.25.0...v0.26.0) (2025-07-10)


### Features

* enhance GetHierarchy with nested JSON and automatic file export ([#185](https://github.com/hatayama/uLoopMCP/issues/185)) ([d2d6957](https://github.com/hatayama/uLoopMCP/commit/d2d6957f8498dd2f48109e9755c1587f7189258f))

## [0.25.0](https://github.com/hatayama/uLoopMCP/compare/v0.24.1...v0.25.0) (2025-07-10)


### Features

* improve RunTests filter types and enhance MCP enum documentation ([#181](https://github.com/hatayama/uLoopMCP/issues/181)) ([fa37b6d](https://github.com/hatayama/uLoopMCP/commit/fa37b6d8e004815cbbd254a7d59fd6585da70739))

## [0.24.1](https://github.com/hatayama/uLoopMCP/compare/v0.24.0...v0.24.1) (2025-07-10)


### Bug Fixes

* update docs ([#177](https://github.com/hatayama/uLoopMCP/issues/177)) ([8eca1bf](https://github.com/hatayama/uLoopMCP/commit/8eca1bf0323be2e069a9d16a06bd3c56b6a9977c))

## [0.24.0](https://github.com/hatayama/uLoopMCP/compare/v0.23.0...v0.24.0) (2025-07-10)


### Features

* add pre-compilation state validation ([#173](https://github.com/hatayama/uLoopMCP/issues/173)) ([71075cb](https://github.com/hatayama/uLoopMCP/commit/71075cb39c96f74bc9aec2d336202b3ef0f163eb))


### Bug Fixes

* enhance error messages for security-blocked commands ([#175](https://github.com/hatayama/uLoopMCP/issues/175)) ([9c48e43](https://github.com/hatayama/uLoopMCP/commit/9c48e4322faa2697b4bc0e065d074b2ad6eba9f4))
* remove unsupported workflow_dispatch from claude actions ([#172](https://github.com/hatayama/uLoopMCP/issues/172)) ([cd8a412](https://github.com/hatayama/uLoopMCP/commit/cd8a412fc3d370146bfaf73a4c3cb45bdff0358a))

## [0.23.0](https://github.com/hatayama/uLoopMCP/compare/v0.22.3...v0.23.0) (2025-07-09)


### Features

* implement security framework with safe-by-default command blocking ([#163](https://github.com/hatayama/uLoopMCP/issues/163)) ([bfaecd3](https://github.com/hatayama/uLoopMCP/commit/bfaecd3344b5072438f491a6b87709f49099672c))

## [0.22.3](https://github.com/hatayama/uLoopMCP/compare/v0.22.2...v0.22.3) (2025-07-09)


### Bug Fixes

* remove PID-based client identification ([#160](https://github.com/hatayama/uLoopMCP/issues/160)) ([f1c7ce1](https://github.com/hatayama/uLoopMCP/commit/f1c7ce117812dc17da7cb8633176606822bf2162))

## [0.22.2](https://github.com/hatayama/uLoopMCP/compare/v0.22.1...v0.22.2) (2025-07-08)


### Bug Fixes

* update README.md ([#150](https://github.com/hatayama/uLoopMCP/issues/150)) ([2031dbe](https://github.com/hatayama/uLoopMCP/commit/2031dbeee29dfb53e9d6706fb3f01e3aa23a7ade))

## [0.22.1](https://github.com/hatayama/uLoopMCP/compare/v0.22.0...v0.22.1) (2025-07-08)


### Bug Fixes

* resolve all TypeScript lint errors and improve type safety ([#157](https://github.com/hatayama/uLoopMCP/issues/157)) ([82c32f6](https://github.com/hatayama/uLoopMCP/commit/82c32f672c20c4cc729fb889b47c77b56127ad66))

## [0.22.0](https://github.com/hatayama/uLoopMCP/compare/v0.21.1...v0.22.0) (2025-07-08)


### Features

* add automated security analysis tools for C# and TypeScript ([#155](https://github.com/hatayama/uLoopMCP/issues/155)) ([fcc7617](https://github.com/hatayama/uLoopMCP/commit/fcc7617281332b820780ef2f8ab746530bcba1c9))

## [0.21.1](https://github.com/hatayama/uLoopMCP/compare/v0.21.0...v0.21.1) (2025-07-08)


### Bug Fixes

* refactor extract server classes for improved maintainability ([#153](https://github.com/hatayama/uLoopMCP/issues/153)) ([0b8c1bc](https://github.com/hatayama/uLoopMCP/commit/0b8c1bc9fe6451b8fa57d4b0fc2d4b405bfc8e45))

## [0.21.0](https://github.com/hatayama/uLoopMCP/compare/v0.20.0...v0.21.0) (2025-07-08)


### Features

* improve connection stability and add universal MCP client support ([#151](https://github.com/hatayama/uLoopMCP/issues/151)) ([a9bec03](https://github.com/hatayama/uLoopMCP/commit/a9bec03045a90133752b54da9f53bec9468eaa6b))

## [0.20.0](https://github.com/hatayama/uLoopMCP/compare/v0.19.2...v0.20.0) (2025-07-06)


### Features

* add camelCase to PascalCase parameter conversion ([#145](https://github.com/hatayama/uLoopMCP/issues/145)) ([d58653c](https://github.com/hatayama/uLoopMCP/commit/d58653c1d2960aa861827060b3334f98331452e5))
* Improvement of tool setting display ([#144](https://github.com/hatayama/uLoopMCP/issues/144)) ([9ffde74](https://github.com/hatayama/uLoopMCP/commit/9ffde747464a8708c05a10ce7a0e4b9f0bc2eb20))


### Bug Fixes

* rename ts directory ([#142](https://github.com/hatayama/uLoopMCP/issues/142)) ([f287adf](https://github.com/hatayama/uLoopMCP/commit/f287adfb47e577c1794c82f18e12b7fd6ba8de25))
* simplify TypeScript server as pure proxy and enable MCP Inspector ([#146](https://github.com/hatayama/uLoopMCP/issues/146)) ([45264bc](https://github.com/hatayama/uLoopMCP/commit/45264bcec25b8810349e99bff0841151b562bf28))
* update command-name, update document  ([#143](https://github.com/hatayama/uLoopMCP/issues/143)) ([96333f9](https://github.com/hatayama/uLoopMCP/commit/96333f95871cfb494ed259cc8f4f0110f2c8fbe6))

## [0.19.2](https://github.com/hatayama/uLoopMCP/compare/v0.19.1...v0.19.2) (2025-07-05)


### Bug Fixes

* prevent "Unknown Client" display on initial connection ([#139](https://github.com/hatayama/uLoopMCP/issues/139)) ([76ae55c](https://github.com/hatayama/uLoopMCP/commit/76ae55c19baa263d5f400c51e8fa9d7112642a93))

## [0.19.1](https://github.com/hatayama/uLoopMCP/compare/v0.19.0...v0.19.1) (2025-07-04)


### Bug Fixes

* remove port number from mcp.json ([#135](https://github.com/hatayama/uLoopMCP/issues/135)) ([bfa3aa3](https://github.com/hatayama/uLoopMCP/commit/bfa3aa3d726355b86abbeea15d4b105be5581391))

## [0.19.0](https://github.com/hatayama/uLoopMCP/compare/v0.18.0...v0.19.0) (2025-07-04)


### Features

* update mcp-setting method ([#132](https://github.com/hatayama/uLoopMCP/issues/132)) ([7b86df9](https://github.com/hatayama/uLoopMCP/commit/7b86df9d1c4492b3f26b748b43f7187ca5d6446a))

## [0.18.0](https://github.com/hatayama/uLoopMCP/compare/v0.17.1...v0.18.0) (2025-07-03)


### Features

* add advanced GameObject search commands with safe component serialization ([#130](https://github.com/hatayama/uLoopMCP/issues/130)) ([8aa3a95](https://github.com/hatayama/uLoopMCP/commit/8aa3a95158945ac7d28b798e2faa4736acfb0373))
* add GetHierarchy command for AI-friendly Unity scene analysis ([#129](https://github.com/hatayama/uLoopMCP/issues/129)) ([4e0fa0b](https://github.com/hatayama/uLoopMCP/commit/4e0fa0b7abf8a361a3e4ccf5ae79e816a1112632))
* implement automatic port adjustment with user confirmation ([#126](https://github.com/hatayama/uLoopMCP/issues/126)) ([849d9a9](https://github.com/hatayama/uLoopMCP/commit/849d9a97b07c5468b7f9c5297f94e495a6daefee))


### Bug Fixes

* update schema ([#128](https://github.com/hatayama/uLoopMCP/issues/128)) ([404b597](https://github.com/hatayama/uLoopMCP/commit/404b597084da3e4bd4ecd2b8de38a0efca4411e4))

## [0.17.1](https://github.com/hatayama/uLoopMCP/compare/v0.17.0...v0.17.1) (2025-07-02)


### Bug Fixes

* debug mode error ([#106](https://github.com/hatayama/uLoopMCP/issues/106)) ([2680924](https://github.com/hatayama/uLoopMCP/commit/2680924d592d8e75fa4d7568ff6f795e02e62595))

## [0.17.0](https://github.com/hatayama/uLoopMCP/compare/v0.16.0...v0.17.0) (2025-07-02)


### Features

* support for wsl2 ([#104](https://github.com/hatayama/uLoopMCP/issues/104)) ([2337d97](https://github.com/hatayama/uLoopMCP/commit/2337d9713a3cc2031370102d67633a2a340ca584))

## [0.16.0](https://github.com/hatayama/uLoopMCP/compare/v0.15.0...v0.16.0) (2025-07-01)


### Features

* add Windsurf editor support and refactor hardcoded .codeium paths ([#102](https://github.com/hatayama/uLoopMCP/issues/102)) ([a51969f](https://github.com/hatayama/uLoopMCP/commit/a51969fb1aa063119a740b7f706b0eaeb29bd2cd))
* implement dynamic client naming using MCP protocol ([#99](https://github.com/hatayama/uLoopMCP/issues/99)) ([7ed004c](https://github.com/hatayama/uLoopMCP/commit/7ed004c9c1b71553abfe662abff2635296c4e7c7))

## [0.15.0](https://github.com/hatayama/uLoopMCP/compare/v0.14.0...v0.15.0) (2025-07-01)


### Features

* add comprehensive TypeScript linting with ESLint and Prettier ([#98](https://github.com/hatayama/uLoopMCP/issues/98)) ([95bd236](https://github.com/hatayama/uLoopMCP/commit/95bd2363014cbc374044260f0da1b1152cc63faf))


### Bug Fixes

* Change log output location ([#96](https://github.com/hatayama/uLoopMCP/issues/96)) ([78746cd](https://github.com/hatayama/uLoopMCP/commit/78746cd15178861c16e8d60698f9aba9812ce23f))

## [0.14.0](https://github.com/hatayama/uLoopMCP/compare/v0.13.0...v0.14.0) (2025-06-29)


### Features

* suport windsurf ([#88](https://github.com/hatayama/uLoopMCP/issues/88)) ([03697fb](https://github.com/hatayama/uLoopMCP/commit/03697fb90b8d63ca8976ec60efb7d506fa25aeef))

## [0.13.0](https://github.com/hatayama/uLoopMCP/compare/v0.12.1...v0.13.0) (2025-06-29)


### Features

* add console clear tool ([#86](https://github.com/hatayama/uLoopMCP/issues/86)) ([31f5d95](https://github.com/hatayama/uLoopMCP/commit/31f5d95a405daaf93d0ef20140b8432ce7d79d9c))
* add unity search ([#84](https://github.com/hatayama/uLoopMCP/issues/84)) ([ee14823](https://github.com/hatayama/uLoopMCP/commit/ee14823c8a6d267ff6823a5521e58697e9e4c340))

## [0.12.1](https://github.com/hatayama/uLoopMCP/compare/v0.12.0...v0.12.1) (2025-06-28)


### Bug Fixes

* display bug in connected server ([#82](https://github.com/hatayama/uLoopMCP/issues/82)) ([0e43825](https://github.com/hatayama/uLoopMCP/commit/0e43825f3ce3cc7ae8d7d5d98e4d57c0c3f760a4))
* window width ([#80](https://github.com/hatayama/uLoopMCP/issues/80)) ([001fc46](https://github.com/hatayama/uLoopMCP/commit/001fc467bfd02da4c71a8e05164d5c83eed30445))

## [0.12.0](https://github.com/hatayama/uLoopMCP/compare/v0.11.0...v0.12.0) (2025-06-27)


### Features

* support gemini cli, mcp inspector ([#77](https://github.com/hatayama/uLoopMCP/issues/77)) ([403bbe8](https://github.com/hatayama/uLoopMCP/commit/403bbe80fb3d13ee621cc1334406205dbc5a1235))

## [0.11.0](https://github.com/hatayama/uLoopMCP/compare/v0.10.0...v0.11.0) (2025-06-27)


### Features

* add  execute menuItem tool, play mode test tool ([#74](https://github.com/hatayama/uLoopMCP/issues/74)) ([a6d2a58](https://github.com/hatayama/uLoopMCP/commit/a6d2a58a0b0eabbcf64ee28101c99d2a9e3c8845))

## [0.10.0](https://github.com/hatayama/uLoopMCP/compare/v0.9.0...v0.10.0) (2025-06-26)


### Features

* Display list of connected LLM tools ([#70](https://github.com/hatayama/uLoopMCP/issues/70)) ([168897d](https://github.com/hatayama/uLoopMCP/commit/168897d31ccddef7040c85e13df14165f0096a34))

## [0.9.0](https://github.com/hatayama/uLoopMCP/compare/v0.8.1...v0.9.0) (2025-06-26)


### Features

* vscode support ([#71](https://github.com/hatayama/uLoopMCP/issues/71)) ([5f8b6c5](https://github.com/hatayama/uLoopMCP/commit/5f8b6c5d7f5d9a0f742bd6eddccf275316e3912c))

## [0.8.1](https://github.com/hatayama/uLoopMCP/compare/v0.8.0...v0.8.1) (2025-06-26)


### Bug Fixes

* Fixed a communication warning that occurred after compilation、node server surviving after closing LLM Tool、Apply UMPC_DEBUG symbol to debug tools, Changed LLM Tool Settins area to be collapsible ([#68](https://github.com/hatayama/uLoopMCP/issues/68)) ([0c6c5d6](https://github.com/hatayama/uLoopMCP/commit/0c6c5d65173bed8d119f9ede8a1baeea130b56a3))

## [0.8.0](https://github.com/hatayama/uLoopMCP/compare/v0.7.0...v0.8.0) (2025-06-25)


### Features

* DelayFrame implementation dedicated to editor ([#59](https://github.com/hatayama/uLoopMCP/issues/59)) ([1369c9f](https://github.com/hatayama/uLoopMCP/commit/1369c9fa852e7cd0bd443940fd3f937755b2ea6b))
* Unify IUnityCommand return type to BaseCommandResponse ([#61](https://github.com/hatayama/uLoopMCP/issues/61)) ([f9b764f](https://github.com/hatayama/uLoopMCP/commit/f9b764f430940fd7d9b994fc5cb24c76dd0efb37))


### Bug Fixes

* Change mcpLogger to scriptable singleton ([#64](https://github.com/hatayama/uLoopMCP/issues/64)) ([9f64778](https://github.com/hatayama/uLoopMCP/commit/9f64778f2be52cf5d9bb5c2e6652c7b1fbc39fe7))
* console masking process ([#63](https://github.com/hatayama/uLoopMCP/issues/63)) ([4cec382](https://github.com/hatayama/uLoopMCP/commit/4cec38206165e2a0ee6b16f5b08bdd645106771b))
* Improved logging using ConsoleLogRetriever ([#67](https://github.com/hatayama/uLoopMCP/issues/67)) ([c06142b](https://github.com/hatayama/uLoopMCP/commit/c06142b6fc0f6578f56cec39b0a0e3ad61f08909))
* Improved timeout time and test result output location ([#55](https://github.com/hatayama/uLoopMCP/issues/55)) ([38ae40b](https://github.com/hatayama/uLoopMCP/commit/38ae40b47894428ff903862cfcf31a11152c772b))
* Remove unnecessary debug logs and improve code quality ([#66](https://github.com/hatayama/uLoopMCP/issues/66)) ([a86f96c](https://github.com/hatayama/uLoopMCP/commit/a86f96c91b2e01d4481fce7b7a95f66e7ed1815f))
* type safe ([#65](https://github.com/hatayama/uLoopMCP/issues/65)) ([3215462](https://github.com/hatayama/uLoopMCP/commit/321546294e6f16b9f20f6f28adec64552c2dfe84))

## [0.7.0](https://github.com/hatayama/uLoopMCP/compare/v0.6.0...v0.7.0) (2025-06-23)


### Features

* getLogs functionality is now supported under unity6. ([#53](https://github.com/hatayama/uLoopMCP/issues/53)) ([344a435](https://github.com/hatayama/uLoopMCP/commit/344a435226cc865470741b5ab9fbd8b1e3320fc7))

## [0.6.0](https://github.com/hatayama/uLoopMCP/compare/v0.5.1...v0.6.0) (2025-06-23)


### Features

* Automatic reconnection after domain reload ([#51](https://github.com/hatayama/uLoopMCP/issues/51)) ([09dfd48](https://github.com/hatayama/uLoopMCP/commit/09dfd48ddaecc9b83bcb1a9edf5df83d36eb2b64))

## [0.5.1](https://github.com/hatayama/uLoopMCP/compare/v0.5.0...v0.5.1) (2025-06-23)


### Bug Fixes

* Improved development mode was not working. ([#48](https://github.com/hatayama/uLoopMCP/issues/48)) ([4735f8d](https://github.com/hatayama/uLoopMCP/commit/4735f8db35410e589893e8d067e221f4f2905890))

## [0.5.0](https://github.com/hatayama/uLoopMCP/compare/v0.4.1...v0.5.0) (2025-06-23)


### Features

* add development mode support for TypeScript server and configur… ([#38](https://github.com/hatayama/uLoopMCP/issues/38)) ([ece81f9](https://github.com/hatayama/uLoopMCP/commit/ece81f919d08b632353665f48e2d2784681acf99))

## [0.4.1](https://github.com/hatayama/uLoopMCP/compare/v0.4.0...v0.4.1) (2025-06-22)


### Bug Fixes

* unity ping response handling ([#33](https://github.com/hatayama/uLoopMCP/issues/33)) ([41b49b0](https://github.com/hatayama/uLoopMCP/commit/41b49b02bde359f6fb41812443fa55a76eb0e0c3))

## [0.4.0](https://github.com/hatayama/uLoopMCP/compare/v0.3.3...v0.4.0) (2025-06-22)


### Features

* implement dynamic tool registration with enhanced Unity communication ([#31](https://github.com/hatayama/uLoopMCP/issues/31)) ([1d5954f](https://github.com/hatayama/uLoopMCP/commit/1d5954fede2b4496a77d1b0c1d12e56d4844acad))

## [0.3.3](https://github.com/hatayama/uLoopMCP/compare/v0.3.2...v0.3.3) (2025-06-21)


### Bug Fixes

* mcp name ([#29](https://github.com/hatayama/uLoopMCP/issues/29)) ([fa411c2](https://github.com/hatayama/uLoopMCP/commit/fa411c204b9f652b76f539a18b20b2659c6994f9))

## [0.3.2](https://github.com/hatayama/uLoopMCP/compare/v0.3.1...v0.3.2) (2025-06-21)


### Bug Fixes

* update description ([#27](https://github.com/hatayama/uLoopMCP/issues/27)) ([6c5bc04](https://github.com/hatayama/uLoopMCP/commit/6c5bc0479aabb55214cee914f9da87e8eb7f85fe))

## [0.3.1](https://github.com/hatayama/uLoopMCP/compare/v0.3.0...v0.3.1) (2025-06-21)


### Bug Fixes

* remove unnecessary variables, etc. ([#26](https://github.com/hatayama/uLoopMCP/issues/26)) ([97aa19a](https://github.com/hatayama/uLoopMCP/commit/97aa19ad92e0fb7664c7c7da37023c056da3f7c5))
* to english ([#24](https://github.com/hatayama/uLoopMCP/issues/24)) ([7027581](https://github.com/hatayama/uLoopMCP/commit/7027581f45b0f0aadfd2996ef670219c4fa372f9))

## [0.3.0](https://github.com/hatayama/uLoopMCP/compare/v0.2.5...v0.3.0) (2025-06-20)


### Features

* add cursor mcp ([#21](https://github.com/hatayama/uLoopMCP/issues/21)) ([f690741](https://github.com/hatayama/uLoopMCP/commit/f69074142a026b053c177e9bcc979d761926a5f8))


### Bug Fixes

* Improvements to log acquisition functions ([#23](https://github.com/hatayama/uLoopMCP/issues/23)) ([081cc83](https://github.com/hatayama/uLoopMCP/commit/081cc83fa82a5a79862952e3d455356a16672bdf))

## [0.2.5](https://github.com/hatayama/uLoopMCP/compare/v0.2.4...v0.2.5) (2025-06-19)


### Bug Fixes

* Improved error handling during server startup ([#19](https://github.com/hatayama/uLoopMCP/issues/19)) ([207b93c](https://github.com/hatayama/uLoopMCP/commit/207b93c51c293c4d01d5c90933c91d3cfd927c42))

## [0.2.4](https://github.com/hatayama/uLoopMCP/compare/v0.2.3...v0.2.4) (2025-06-19)


### Bug Fixes

* js-tools for debug ([#17](https://github.com/hatayama/uLoopMCP/issues/17)) ([00abe45](https://github.com/hatayama/uLoopMCP/commit/00abe45d106de70a081009a375e4873a063e2172))

## [0.2.3](https://github.com/hatayama/uLoopMCP/compare/v0.2.2...v0.2.3) (2025-06-18)


### Bug Fixes

* auto restart ([#15](https://github.com/hatayama/uLoopMCP/issues/15)) ([6947c49](https://github.com/hatayama/uLoopMCP/commit/6947c490ee3b39fd558c83bb0f8146e96e792b30))

## [0.2.2](https://github.com/hatayama/uLoopMCP/compare/v0.2.1...v0.2.2) (2025-06-17)


### Bug Fixes

* uniform variable names ([#12](https://github.com/hatayama/uLoopMCP/issues/12)) ([37ca79a](https://github.com/hatayama/uLoopMCP/commit/37ca79a6c30db82606cc026dcd13fdf9a92299d8))

## [0.2.1](https://github.com/hatayama/uLoopMCP/compare/v0.2.0...v0.2.1) (2025-06-17)


### Bug Fixes

* update readme ([#10](https://github.com/hatayama/uLoopMCP/issues/10)) ([cff429b](https://github.com/hatayama/uLoopMCP/commit/cff429b86bc5ae92dd4e7750e87cbd4e2bbcbfa2))

## [0.2.0](https://github.com/hatayama/uLoopMCP/compare/v0.1.1...v0.2.0) (2025-06-17)


### Features

* Compatible with Test Runner And Claude Code ([#8](https://github.com/hatayama/uLoopMCP/issues/8)) ([bf06ab3](https://github.com/hatayama/uLoopMCP/commit/bf06ab324f57c0c36474e6b56569a498f3cfb36a))

## 0.1.1 (2025-06-17)


### Bug Fixes

* organize menuItems ([ebdfeee](https://github.com/hatayama/uLoopMCP/commit/ebdfeee4f9aaa2b84e1d974d7c5e85eb96670a37))
* package.json ([f769d23](https://github.com/hatayama/uLoopMCP/commit/f769d2318c9d069337e945f295bb90348bfa6572))
* fix server file lookup ([6df144e](https://github.com/hatayama/uLoopMCP/commit/6df144e232edf021e642a319e4b57a1813c216ae))
* update readme ([1d36d6e](https://github.com/hatayama/uLoopMCP/commit/1d36d6ef58f65ff3c01e3769ea674dcec7c2fe85))


### Miscellaneous Chores

* release 0.1.1 ([ccaa515](https://github.com/hatayama/uLoopMCP/commit/ccaa51573cf2310448692f4d5e63406bd6de4c36))

## [0.1.0] - 2024-04-07

### Added
- Initial release
- Basic functionality to bind Inspector values to other components
- Sample scene

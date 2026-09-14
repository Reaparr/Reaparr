Reaparr Changelog

# [0.40.0-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.40.0-dev.1...v0.40.0-dev.2) (2026-09-14)


### Bug Fixes

* **WebAPI:** Align paging and rebuild expectations ([b2a3665](https://github.com/Reaparr/Reaparr/commit/b2a3665ea1ce016e95c994fb10e2c2154038b25d))
* **WebAPI:** Align Torznab branch ordering ([ec02f3e](https://github.com/Reaparr/Reaparr/commit/ec02f3e92f57bcbbb01a82bc1fb129e7ecbce35f))
* apply CodeRabbit auto-fixes ([9348147](https://github.com/Reaparr/Reaparr/commit/934814793c5c31672fb15efcada84e13102adc58))
* **WebAPI:** Apply configured umask to Reaparr service ([5c4c1b3](https://github.com/Reaparr/Reaparr/commit/5c4c1b3a065ef7784fcd50257f8424dd9dfdf9ae))
* **Web-UI:** Centralize refresh notification debounce ([705320b](https://github.com/Reaparr/Reaparr/commit/705320bfd99b1df57a14768bd65affcf5e588bda))
* **WebAPI:** Order Torznab branches before paging ([ac23ce4](https://github.com/Reaparr/Reaparr/commit/ac23ce47752e9b8a8185a5f9ab625fa41fb078ec))
* **WebAPI:** Prevent unnecessary rebuilds when not dirty ([db83746](https://github.com/Reaparr/Reaparr/commit/db83746c258b51ad86d0aaf2be08efbd87de6dcf))
* **WebAPI:** Report restored library access as granted ([6622bb2](https://github.com/Reaparr/Reaparr/commit/6622bb287f8bf2169921ae564ac3185b1691873c))
* **WebAPI:** Stabilize dispatcher sequence test ([5a97dd1](https://github.com/Reaparr/Reaparr/commit/5a97dd118e83688f81e8299c12ddc3e0b76f1d41))
* **WebAPI:** Update authentication token handling in account dialog ([6075330](https://github.com/Reaparr/Reaparr/commit/607533087b5b7126f506fafc999cb521bee0f280))


### Features

* **Web-UI:** Add media load error handling and retry functionality ([598ebe0](https://github.com/Reaparr/Reaparr/commit/598ebe0e85048bf78e1635cfc7360e21d9fab605))
* **WebAPI:** Implement genre type recalculation command and handler on startup ([b1223c8](https://github.com/Reaparr/Reaparr/commit/b1223c8167c414488e566404b658c0cc42523246))
* **WebAPI:** Introduce PlexGenreType enum and extensions for genre mapping ([afdc67e](https://github.com/Reaparr/Reaparr/commit/afdc67ef11106dd28d91e90ea4c98388cf142e6b))


### Performance Improvements

* **WebAPI:** Optimize media overview commands for movies and TV shows ([f2ab88b](https://github.com/Reaparr/Reaparr/commit/f2ab88ba6c8f859cbd778f788168ff28322640e3))

# [0.40.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.39.0...v0.40.0-dev.1) (2026-09-11)


### Bug Fixes

* **Web-UI:** Add validation messages for unique category and base URL in integration setup ([efef224](https://github.com/Reaparr/Reaparr/commit/efef22450d6f3a5a5562d7c7947684db4518759b))
* **Web-UI:** Center align help row components and enhance integration dialog layout ([166809e](https://github.com/Reaparr/Reaparr/commit/166809e6292e8207e5691e5befc6a7994f7f99f2))
* **WebAPI:** Correct integration ownership filtering ([5e857cb](https://github.com/Reaparr/Reaparr/commit/5e857cb3e757c3f5b22ccb66bf3c1344fade4d51))
* **Web-UI:** Enable individual notification dismissal ([fc84f63](https://github.com/Reaparr/Reaparr/commit/fc84f6306aeb43a8b52ef99312ddd5749a40d36e))
* **WebAPI:** Exclude servers with paused downloads from indexer results ([#653](https://github.com/Reaparr/Reaparr/issues/653)) ([410fec0](https://github.com/Reaparr/Reaparr/commit/410fec098623e0dce06b9fe48efa475322270a32))
* **WebAPI:** Exclude servers with paused downloads from indexer results ([9b007a5](https://github.com/Reaparr/Reaparr/commit/9b007a537efd5270cc578180150f482f8d9faa5b))
* **Web-UI:** Fixed missing strikethrough effect on Plex servers whose access has been lost ([fa36cc4](https://github.com/Reaparr/Reaparr/commit/fa36cc47409fba8f84e2ad828e376a26321de7df))
* **WebAPI:** Fixed Radarr/Sonarr returning media results from Plex servers your dont have access to anymore ([fb9a614](https://github.com/Reaparr/Reaparr/commit/fb9a6145b72219e116593109b03dde1ed480e006))
* **WebAPI:** Fixed the integration check connection always using what is stored instead of what the user is currently trying ([236966a](https://github.com/Reaparr/Reaparr/commit/236966a73455460ba1a647ad594f85f0f8722c10))
* **WebAPI:** Format named result messages ([53b7a0a](https://github.com/Reaparr/Reaparr/commit/53b7a0af55c6904fa47412930328fe9874e7df38))
* **Web-UI:** Handle 204 responses and null data in API pipes ([4fc0a4a](https://github.com/Reaparr/Reaparr/commit/4fc0a4acfec8dc6234d3abbcbc1d2877ffd5b058))
* **WebAPI:** Handle raw background job payloads ([c1b5989](https://github.com/Reaparr/Reaparr/commit/c1b5989f68b1e97b0356657d959dec9b7c3332dc))
* **WebAPI:** Harden CI test artifacts and release workflows ([6862256](https://github.com/Reaparr/Reaparr/commit/6862256815bc5de13d169c75b864dab7121dcd60))
* **WebAPI:** Implement the generic Torznab search query type ([#652](https://github.com/Reaparr/Reaparr/issues/652)) ([44879c2](https://github.com/Reaparr/Reaparr/commit/44879c2b593f49fd548aa6c6249a05e26e9c79bd))
* **WebAPI:** Implement the generic Torznab search query type ([25ba0ab](https://github.com/Reaparr/Reaparr/commit/25ba0ab076ed0a7604aecc626de69b0482bbad46)), closes [#650](https://github.com/Reaparr/Reaparr/issues/650)
* **Web-UI:** Improve error handling for integration save and setup processes ([cd2b922](https://github.com/Reaparr/Reaparr/commit/cd2b92232d14fe09fe094114ee56fbae58a01af4))
* **WebAPI:** Preserve custom download categories ([48a49c2](https://github.com/Reaparr/Reaparr/commit/48a49c280cf510da8af4b9d2e4f6b998cf24489e))
* **WebAPI:** Rename reserved desktop workflow token secret ([134228b](https://github.com/Reaparr/Reaparr/commit/134228bf7815a788839fce68d951afdc98ba069c))
* **WebAPI:** Restrict reusable build workflow permissions ([eb7aa60](https://github.com/Reaparr/Reaparr/commit/eb7aa601d606d5604efde70d03d3e386afffadee))
* **WebAPI:** Run semantic release from repository root ([9fc0b93](https://github.com/Reaparr/Reaparr/commit/9fc0b93916e59befd95b5417e3fe149a3452a13e))
* **WebAPI:** Serialize database test setup ([e3b6560](https://github.com/Reaparr/Reaparr/commit/e3b6560b120f6adbb62b8af6082b59696419252b))
* **WebAPI:** Support Sonarr season-only searches ([#551](https://github.com/Reaparr/Reaparr/issues/551)) ([df263d4](https://github.com/Reaparr/Reaparr/commit/df263d418fe54b55f551ae6915f68795f364887b))
* **WebAPI:** Update integration status after successful connection test ([2d034d4](https://github.com/Reaparr/Reaparr/commit/2d034d4137d6a4bdbfa7bec95f75265319ed6376))
* **Web-UI:** Update integration test dialog messages and UI alerts ([efc6eef](https://github.com/Reaparr/Reaparr/commit/efc6eef768d9fd1b3b649acaaacbe3425e18208f))
* **Web-UI:** Update validation messages for integration setup ([6d0ebf9](https://github.com/Reaparr/Reaparr/commit/6d0ebf903b79285cf7f3a3a5afc5b7b3a856fd2c))
* **WebAPI:** Use enabled SSL option in Downloadclient when setting up Radarr/Sonarr configuration ([71ee4b0](https://github.com/Reaparr/Reaparr/commit/71ee4b0f10e110a239267058dce2e65ac735aae5))
* **WebAPI:** Use published Cypress action tag ([cd8862e](https://github.com/Reaparr/Reaparr/commit/cd8862e9e4cb18f23607569d37a8c44c940dba13))


### Features

* **WebAPI:** Add auto migration for Sonarr/Radarr new api key system ([02d46ca](https://github.com/Reaparr/Reaparr/commit/02d46cab30bcbdf62b23e6361668a3174cb266f1))
* **Web-UI:** Add connection status indicators for integrations cards ([b1bd4c2](https://github.com/Reaparr/Reaparr/commit/b1bd4c21011ecddceed62455cf1600e8abc9cfa5))
* **WebAPI:** Add connection test functionality for Radarr and Sonarr integrations on Reaparr start-up ([1ae10ab](https://github.com/Reaparr/Reaparr/commit/1ae10abb9f97e37aa58d2af2e1ced38e1668a77b))
* **Web-UI:** Add delete integration confirmation dialog ([5fc05f6](https://github.com/Reaparr/Reaparr/commit/5fc05f62d9a9ab6c4d6a23516aac1384e277ba95))
* **WebAPI:** Add multi-integration support for Radarr and Sonarr ([4663459](https://github.com/Reaparr/Reaparr/commit/466345962b1ba724129b2f3d0932dfa41c5b4ff1))
* **Web-UI:** Add save and setup button to integration dialogs ([dd1aaa9](https://github.com/Reaparr/Reaparr/commit/dd1aaa901219d3ad351e3610268ed0d484346ed3))
* **Web-UI:** Add validation step to integration setup process ([c66f860](https://github.com/Reaparr/Reaparr/commit/c66f860480dd1a0fe4d6f77bad2b490a30b72018))
* **Web-UI:** Enhance folder path management with new tab UI elements and allow adding custom download paths ([4c18be7](https://github.com/Reaparr/Reaparr/commit/4c18be78e761e41d1af094064af2156b9280543b))
* **Web-UI:** Implement integration management UI with overview and setup dialogs ([1f72f65](https://github.com/Reaparr/Reaparr/commit/1f72f65192c1b9c7844903b66d3f2311cf725bab))
* **WebAPI:** Implement integration setup progress tracking for Radarr and Sonarr ([c3cc659](https://github.com/Reaparr/Reaparr/commit/c3cc659bf5e11b6ff61b347af2f19566dff378a4))


### Performance Improvements

* **WebAPI:** Discover backend tests dynamically ([c516282](https://github.com/Reaparr/Reaparr/commit/c5162822b17d50728c4c9e5b0109e888228c065d))
* **WebAPI:** Enhance comparison logic for TV shows, seasons, and episodes ([4f5d467](https://github.com/Reaparr/Reaparr/commit/4f5d4671111f1f5f64b09d976eb8ec566afb2f0d))
* **WebAPI:** Update project references and improve database setup handling during testing ([f223944](https://github.com/Reaparr/Reaparr/commit/f2239449469bae8e0677e5ef99bd4d5cd8a6359e))

# [0.40.0-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.40.0-dev.1...v0.40.0-dev.2) (2026-09-08)


### Bug Fixes

* **WebAPI:** Implement the generic Torznab search query type ([#652](https://github.com/Reaparr/Reaparr/issues/652)) ([44879c2](https://github.com/Reaparr/Reaparr/commit/44879c2b593f49fd548aa6c6249a05e26e9c79bd))
* **WebAPI:** Implement the generic Torznab search query type ([25ba0ab](https://github.com/Reaparr/Reaparr/commit/25ba0ab076ed0a7604aecc626de69b0482bbad46)), closes [#650](https://github.com/Reaparr/Reaparr/issues/650)

# [0.40.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.39.0...v0.40.0-dev.1) (2026-09-08)


### Bug Fixes

* **WebAPI:** Exclude servers with paused downloads from indexer results ([#653](https://github.com/Reaparr/Reaparr/issues/653)) ([410fec0](https://github.com/Reaparr/Reaparr/commit/410fec098623e0dce06b9fe48efa475322270a32))
* **WebAPI:** Exclude servers with paused downloads from indexer results ([9b007a5](https://github.com/Reaparr/Reaparr/commit/9b007a537efd5270cc578180150f482f8d9faa5b))
* **Web-UI:** Fixed missing strikethrough effect on Plex servers whose access has been lost ([fa36cc4](https://github.com/Reaparr/Reaparr/commit/fa36cc47409fba8f84e2ad828e376a26321de7df))


### Features

* **WebAPI:** Add auto migration for Sonarr/Radarr new api key system ([02d46ca](https://github.com/Reaparr/Reaparr/commit/02d46cab30bcbdf62b23e6361668a3174cb266f1))
* **Web-UI:** Add connection status indicators for integrations cards ([b1bd4c2](https://github.com/Reaparr/Reaparr/commit/b1bd4c21011ecddceed62455cf1600e8abc9cfa5))
* **WebAPI:** Add connection test functionality for Radarr and Sonarr integrations on Reaparr start-up ([1ae10ab](https://github.com/Reaparr/Reaparr/commit/1ae10abb9f97e37aa58d2af2e1ced38e1668a77b))
* **Web-UI:** Add delete integration confirmation dialog ([5fc05f6](https://github.com/Reaparr/Reaparr/commit/5fc05f62d9a9ab6c4d6a23516aac1384e277ba95))
* **WebAPI:** Add multi-integration support for Radarr and Sonarr ([4663459](https://github.com/Reaparr/Reaparr/commit/466345962b1ba724129b2f3d0932dfa41c5b4ff1))
* **Web-UI:** Add validation step to integration setup process ([c66f860](https://github.com/Reaparr/Reaparr/commit/c66f860480dd1a0fe4d6f77bad2b490a30b72018))
* **Web-UI:** Enhance folder path management with new tab UI elements and allow adding custom download paths ([4c18be7](https://github.com/Reaparr/Reaparr/commit/4c18be78e761e41d1af094064af2156b9280543b))
* **Web-UI:** Implement integration management UI with overview and setup dialogs ([1f72f65](https://github.com/Reaparr/Reaparr/commit/1f72f65192c1b9c7844903b66d3f2311cf725bab))
* **WebAPI:** Implement integration setup progress tracking for Radarr and Sonarr ([c3cc659](https://github.com/Reaparr/Reaparr/commit/c3cc659bf5e11b6ff61b347af2f19566dff378a4))


### Performance Improvements

* **WebAPI:** Enhance comparison logic for TV shows, seasons, and episodes ([4f5d467](https://github.com/Reaparr/Reaparr/commit/4f5d4671111f1f5f64b09d976eb8ec566afb2f0d))

# [0.39.0](https://github.com/Reaparr/Reaparr/compare/v0.38.3...v0.39.0) (2026-08-26)


### Bug Fixes

* **WebAPI:** Add UMASK environment variable for group-writable files ([6d43c92](https://github.com/Reaparr/Reaparr/commit/6d43c9246fd5c1e09c4d5581f16675592001b996))
* **Web-UI:** Adjust column widths in downloads table ([41c1a9d](https://github.com/Reaparr/Reaparr/commit/41c1a9d405b09fee8d73b9aeac2faf27c33b288c))
* **WebAPI:** Align ToParentState Missing/Partial semantics and test matching ([795a281](https://github.com/Reaparr/Reaparr/commit/795a2810ab56ab8ea5af946eec4e57f23f278e8c))
* apply CodeRabbit auto-fixes ([3bf6754](https://github.com/Reaparr/Reaparr/commit/3bf67549430e52c7f0d81dbefff2290013589457))
* **WebAPI:** Auto cancel a Plex Library when the access token is invalidated mid-run ([c418332](https://github.com/Reaparr/Reaparr/commit/c4183329ac9bbbbe086bf89cb636e73b371f6450))
* **WebAPI:** avoid duplicate queued jobs ([948794f](https://github.com/Reaparr/Reaparr/commit/948794f27dfe05f2e8c9fec6ae4becf44b132dc3))
* **Web-UI:** Correct store import usage ([f1bc11e](https://github.com/Reaparr/Reaparr/commit/f1bc11edd99170258e30d8a2d1dc950b03067f73))
* **WebAPI:** Correct TV comparison state filtering ([a9e547d](https://github.com/Reaparr/Reaparr/commit/a9e547dfeef2eea3ccdbf2ea39da59f1062df070))
* **WebAPI:** Deduplicate Plex library refresh rows ([d38720d](https://github.com/Reaparr/Reaparr/commit/d38720d6c6d058be070afc0fb9a08dd7f22b8f28))
* **Web-UI:** Disable cancel button when deleting a server ([9a027e3](https://github.com/Reaparr/Reaparr/commit/9a027e30e80d27cc0a7c95e35386318cfdc53f4b))
* **Web-UI:** Display ETA in library media sync dialog ([0f0e163](https://github.com/Reaparr/Reaparr/commit/0f0e16356eacd744519b7055ff405cfb8cd76d84))
* **WebAPI:** Downloading from a server which has not auth token anymo… ([#649](https://github.com/Reaparr/Reaparr/issues/649)) ([5b7becd](https://github.com/Reaparr/Reaparr/commit/5b7becdf25a5058366d6737ff24a3b53db6ba501))
* **WebAPI:** Downloading from a server which has not auth token anymore will now correctly make as AuthError ([6519804](https://github.com/Reaparr/Reaparr/commit/6519804a9c7651281aeb7d6be8abfadac60cc500))
* **Web-UI:** Enhance download notification translations for TV shows, seasons, and episodes ([3deb5a7](https://github.com/Reaparr/Reaparr/commit/3deb5a788d0db93fc42453e1a23d28b45f669fc9))
* **WebAPI:** Enhance download task retry logic and status handling ([981c712](https://github.com/Reaparr/Reaparr/commit/981c712ea2759866a3643bdf46922b94f2344695))
* **WebAPI:** Enhance media query cache initialization before background jobs ([d6bd17a](https://github.com/Reaparr/Reaparr/commit/d6bd17af40f5883eb290611fca6f7beddcf443a2))
* **WebAPI:** Exclude disabled libraries from comparison queueing in SetServerOwned ([9b492c7](https://github.com/Reaparr/Reaparr/commit/9b492c70779aa5e692dbe672608e378a3b3260a4))
* **WebAPI:** Fix concurrency group to prevent reusable workflow job cancellation ([9b12d93](https://github.com/Reaparr/Reaparr/commit/9b12d934de5bea62523791b10ef08bf197ae688e))
* **WebAPI:** Fix exception: resume only when the Direct Download snapshot contains at least one chunk ([051cd2f](https://github.com/Reaparr/Reaparr/commit/051cd2f014c05e4788d78f9ee793b00dc8d3c537))
* **Web-UI:** Fix poster hover overlay for items with missing thumbnails ([cce751b](https://github.com/Reaparr/Reaparr/commit/cce751badc02995d470d4518cc04e16ba12befa1))
* **WebAPI:** Fix stopping scheduler first ([bfab6d0](https://github.com/Reaparr/Reaparr/commit/bfab6d01e9a18acf24a80a8bead44322f2474c29))
* **Web-UI:** Fix the download confirmation destination menu not working when there are no custom paths ([6233562](https://github.com/Reaparr/Reaparr/commit/62335624099ade7df12a22d31c339f33c3ff3fe1))
* **Web-UI:** Fix the flashing of not synced media in media overview while it was still loading ([ab0bbca](https://github.com/Reaparr/Reaparr/commit/ab0bbcaef933fe0955d5667a15b27418fa5090f7))
* **WebAPI:** Fix the missing ETA when downloaded files are moved ([49d432f](https://github.com/Reaparr/Reaparr/commit/49d432f76b9b9ae1bd8199a65c3f1958a4065ceb))
* **WebAPI:** Fixed an exceptions when a disabled Plex library is re-synced ([13c08ad](https://github.com/Reaparr/Reaparr/commit/13c08ad22b43a4d7ea6215f523c6a3d03fa7b0d5))
* **WebAPI:** Fixed an Plex server returning an unauthorized response when loading thumbnails ([0974078](https://github.com/Reaparr/Reaparr/commit/09740789bb7c12287e87207cad5e6cd5621bc34c))
* **WebAPI:** Fixed incorrect default Plex library destination assinged to Music and Photo's libraries ([432b3aa](https://github.com/Reaparr/Reaparr/commit/432b3aaabad5eb215296db3d81826e7fb48ff693))
* **Web-UI:** Fixed missing progress when the CheckAllConnectionsStatusByPlexServerJob runs in the dialog ([fc79563](https://github.com/Reaparr/Reaparr/commit/fc7956331d8b127fcf6974a47e2b16df20134d57))
* **Web-UI:** Fixed the clear completion dialog clearing the wrong Download table of completed download tasks ([c198a5c](https://github.com/Reaparr/Reaparr/commit/c198a5c33bbd356ab679c17c7db4b2cc8f195018))
* **Web-UI:** Fixed the issue where all Plex servers were shown when re-syncing the Plex Libraries from the Plex Server settings ([cba0f76](https://github.com/Reaparr/Reaparr/commit/cba0f76b61ea6d63b3a9e091b88cf86ccb43a7bf))
* **Web-UI:** Fixed the media overview not returning to the last scroll position ([793bb4a](https://github.com/Reaparr/Reaparr/commit/793bb4ad6c48b533af7dcf90a269b9bcf68b82da))
* **WebAPI:** Fixed the missing quality options in the media filter ([89aed96](https://github.com/Reaparr/Reaparr/commit/89aed9616dafb55b530ddc94718d4d821b4d0f9f))
* **Web-UI:** Fixed the overflow of long titles which now dynamicy sizes the title based on its length in media posters ([10370b2](https://github.com/Reaparr/Reaparr/commit/10370b2d4e228ca506ed1aa31a025679083673ed))
* **Web-UI:** Fixed the Plex account refresh menu showing all accounts as loading when only 1 account has been requested to be refreshed ([56e008d](https://github.com/Reaparr/Reaparr/commit/56e008d2a1b40408fd23bf14c001a08f9b9dd969))
* **WebAPI:** Fixed tv-show/season download task not showing the total ETA ([2dc326a](https://github.com/Reaparr/Reaparr/commit/2dc326a52f4febb310fd30a8d9082218d5fb02ea))
* **WebAPI:** Flatten comparison detail episodes ([4440133](https://github.com/Reaparr/Reaparr/commit/444013385cb934dff11b23b09c8779c4c78d0956))
* **WebAPI:** Handle empty thumbnail responses ([5fb8557](https://github.com/Reaparr/Reaparr/commit/5fb85577426fa79e13c8d1978f5cdcbca85de6d4))
* **WebAPI:** Handle LibrarySync job cancellation when state changes during deletion ([ad54f0d](https://github.com/Reaparr/Reaparr/commit/ad54f0d0e4005a70f18d64b0906f3651022f9dd6))
* **WebAPI:** Improve cancellation handling in download tasks ([d1c2be9](https://github.com/Reaparr/Reaparr/commit/d1c2be971bb2110d7ae8025421c898b5a4d239da))
* **WebAPI:** Increase database retry interval for Quartz jobs ([226485c](https://github.com/Reaparr/Reaparr/commit/226485c50bca40aba7a75b14120f649350a1e44c))
* **background-jobs:** isolate TickerQ execution context ([28c0fc8](https://github.com/Reaparr/Reaparr/commit/28c0fc8ac2e0be0debc8e2a85bed5c09fdb0e88b))
* **WebAPI:** Mark library sync as completed before continiung with comparision job ([c8d794c](https://github.com/Reaparr/Reaparr/commit/c8d794cf5f205f19b86368fc985e1ebddf4b3dce))
* **Web-UI:** Normalize scrollbar styling ([d03a2af](https://github.com/Reaparr/Reaparr/commit/d03a2afa8f0ec11e5de4ce99c9572dbd4e740bb1))
* **Web-UI:** Only show relevant library sync jobs in the SyncServerMediaDialog.vue when opened ([4222ed1](https://github.com/Reaparr/Reaparr/commit/4222ed1eb28fcb6bcdb6e5fca67e08fc7a035437))
* **WebAPI:** Only use Plex accounts that are still valid ([e297942](https://github.com/Reaparr/Reaparr/commit/e297942cd02d52662ea68b38f12bbadc91edd353))
* **WebAPI:** Optimize Plex server deletion logic ([f501fb4](https://github.com/Reaparr/Reaparr/commit/f501fb47e1d0bcf54bd455baeda6e2b742cc42c9))
* **WebAPI:** persist Plex token validation state ([e00bfce](https://github.com/Reaparr/Reaparr/commit/e00bfce566b812b869afeab256da4738d226ed54))
* preserve reparented TV hierarchy during sync ([b949f3c](https://github.com/Reaparr/Reaparr/commit/b949f3cca2e20233cb79cc9348ce2548630e3e48))
* **WebAPI:** Prevent duplicate library comparison job triggers ([588a1bc](https://github.com/Reaparr/Reaparr/commit/588a1bc0d1fe28615b474428f7957f3f2f188c62))
* **WebAPI:** Prevent duplicate library sync execution ([d152b8c](https://github.com/Reaparr/Reaparr/commit/d152b8c37d4876ec991b3233806d86091d7cb218))
* **Web-UI:** Prevent empty media flash during filter refresh ([721a761](https://github.com/Reaparr/Reaparr/commit/721a7614ba0efff87ce27b63e770379c7365baeb))
* **WebAPI:** Project TV show detail comparison state ([3320857](https://github.com/Reaparr/Reaparr/commit/3320857afa3f0dcbb27c2cea48800565dbae1a09))
* **WebAPI:** queue sync for changed plex libraries ([1b89ea0](https://github.com/Reaparr/Reaparr/commit/1b89ea097f2b45cecfa40c43558eff5c19cd90cf))
* **WebAPI:** Re-implement retry logic for download tasks especially when the connection is poor ([646b805](https://github.com/Reaparr/Reaparr/commit/646b805433607545f52832b3c4b885861b845b05))
* **WebAPI:** Recompute TV library sync metrics ([ce50624](https://github.com/Reaparr/Reaparr/commit/ce50624814314377942e43414b31dc1542343d54))
* **WebAPI:** Remove concurrency block to fix reusable workflow job cancellation ([9d27992](https://github.com/Reaparr/Reaparr/commit/9d279922f9dcc122182ab1f1f6fd90d0667a08c5))
* **WebAPI:** Remove library access for revoked servers ([94308a6](https://github.com/Reaparr/Reaparr/commit/94308a67475a81673d671f79a3128398026bcdca))
* **Web-UI:** Requeue enabled unsynced libraries ([36d7da1](https://github.com/Reaparr/Reaparr/commit/36d7da14c2ac443a15afa2f38cbfcf8de80bb926))
* **WebAPI:** Requeue enabled unsynced libraries ([c213405](https://github.com/Reaparr/Reaparr/commit/c21340572b113a24daaad26c4148e431b3ebde20))
* **WebAPI:** Require requests for queued background jobs ([ae0d9e3](https://github.com/Reaparr/Reaparr/commit/ae0d9e3757ecd1ed0a179e92dfa9d01524af76df))
* **Web-UI:** Reset media navigation indexes on query change ([ed0f5c5](https://github.com/Reaparr/Reaparr/commit/ed0f5c5eec8a01b29dd0843f8bf61967650c23e3))
* resolve cancellation pipeline failures ([852bcc6](https://github.com/Reaparr/Reaparr/commit/852bcc6f100e9d84656a48fdb33abe67e62c019b))
* **Web-UI:** Restore server access refresh flow ([f30cd32](https://github.com/Reaparr/Reaparr/commit/f30cd3279b448360dc0b56e2bd41c0afa8fd8f59))
* **WebAPI:** Restore transient download retries ([6c2c844](https://github.com/Reaparr/Reaparr/commit/6c2c8448f222f4a6f260c353ff277d2a09e39e3c))
* **data:** return results from serialized transactions ([22a6669](https://github.com/Reaparr/Reaparr/commit/22a66696b289ba7f8af8f3cbf96348e0d951bcfa))
* **WebAPI:** Show pending comparison state while queued ([c2d6b51](https://github.com/Reaparr/Reaparr/commit/c2d6b519da4cc2e7949e7185796b41071e341553))
* **Web-UI:** Stabilize comparison details dialog rows ([c54bb07](https://github.com/Reaparr/Reaparr/commit/c54bb07550bf81da28bbcc1dd2b0fb5513a9e86e))
* **WebAPI:** Support shared in-memory SQLite databases ([607bce1](https://github.com/Reaparr/Reaparr/commit/607bce1b2b7c3d60644be4d7a5f91ba70f4f692f))
* **WebAPI:** Update cancellation handling and improve unit tests for download tasks recovery ([d6f0401](https://github.com/Reaparr/Reaparr/commit/d6f0401f0f1bc4bbb269affe8fc7778ea90d9e77))
* **WebAPI:** Update time remaining calculation for moving download tasks ([ca02598](https://github.com/Reaparr/Reaparr/commit/ca02598a79a0edd7c0c278fa6e82ee57d26666ad))


### Features

* **WebAPI:** Add comparison state to TV show detail endpoint ([b45d315](https://github.com/Reaparr/Reaparr/commit/b45d3154dd0d8a5be1860bf2f2af295cf36cbcec))
* **WebAPI:** Add ComparisonState filter for library comparison ([8aafbc6](https://github.com/Reaparr/Reaparr/commit/8aafbc6db1bd3fe7a1edd04d3588b2f5655638c7))
* **WebAPI:** Add library enable/disable support ([52a38b4](https://github.com/Reaparr/Reaparr/commit/52a38b48cf6487eda1a944ee8887c8cdc596ce0e))
* **Web-UI:** Add media refresh mode dialog ([b456f5b](https://github.com/Reaparr/Reaparr/commit/b456f5b6b2d1eb5539863d0bb7e00380f6412d2e))
* **Web-UI:** Add plex media comparison dialog to showcase what and how you can upgrade a media item ([f184617](https://github.com/Reaparr/Reaparr/commit/f1846177a8c3094cca08bb3625524d752dcda46d))
* **WebAPI:** Add seperate refresh access endpoint for Plex server accounts ([3f1fc47](https://github.com/Reaparr/Reaparr/commit/3f1fc4777dfa28a8a9a343526f33290df77f0f53))
* **Web-UI:** Add the selected destination in the download confirmation dialog next to the download button ([ef7cea5](https://github.com/Reaparr/Reaparr/commit/ef7cea53d1877491fecbbc1010ddf99674610324))
* **Web-UI:** Added a banner when a library has lost access ([5ca5018](https://github.com/Reaparr/Reaparr/commit/5ca50184e8aa6578d3b0acabfcaabf959f3c4171))
* **Web-UI:** Added enable/disable settings for Plex libraries in the server settings ([c25d8c1](https://github.com/Reaparr/Reaparr/commit/c25d8c12d25fe1ce47fb332fb7b015247947aaff))
* **Web-UI:** Added media comparison column to the TvShow detail screen ([02cd546](https://github.com/Reaparr/Reaparr/commit/02cd546658b1af5a83a35f5170afe834bb82136e))
* **Web-UI:** Added media comparison dialog showing what you can upgrade or is missing from your own tv-shows ([1090b1c](https://github.com/Reaparr/Reaparr/commit/1090b1ca581dc1165c1fcae8c4001d267f22c3ad))
* **Web-UI:** Added media comparison filter ([a0a97dc](https://github.com/Reaparr/Reaparr/commit/a0a97dcbda82f0ffdfcad2cb7ee0170048cccb81))
* **Web-UI:** Added movie detail screen so you can see details about the movie before downloading ([1ce622d](https://github.com/Reaparr/Reaparr/commit/1ce622dbc669d1265fee1cdf1b896e7a8848e06b))
* **WebAPI:** Added refresh Plex Account job that will run every 6 hours to check if the Plex account is still valid ([4e96e09](https://github.com/Reaparr/Reaparr/commit/4e96e09d2f2261a655a6b71be961ac47a70e753c))
* **WebAPI:** Library media sync will now sync incrementally and upda… ([#643](https://github.com/Reaparr/Reaparr/issues/643)) ([6660c09](https://github.com/Reaparr/Reaparr/commit/6660c0957ad0000ed89158550a718b68d4a8c48a))
* **WebAPI:** Library media sync will now sync incrementally and update the database only with what has changed by default. This should save SSD writes ([b8827c1](https://github.com/Reaparr/Reaparr/commit/b8827c18a21ee3874a89359c055ac78d66040af2))
* **Web-UI:** Media highlight the last viewed media when navigating ([a1af839](https://github.com/Reaparr/Reaparr/commit/a1af83969e27627ba58a7a4d2096952c04ecf7db))
* **WebAPI:** Send notification to front-end when a Plex library is done with comparing such that the front-end can refresh its data ([068a8dd](https://github.com/Reaparr/Reaparr/commit/068a8dd3ca3bac7a1775234cc29342f8bc8abce4))
* **Web-UI:** Show a yellow alert on Plex library media overview which access has been lost/revoked ([1099677](https://github.com/Reaparr/Reaparr/commit/10996773c92e6f45179560738124cc145ba36232))
* **Web-UI:** Will auto-load the current library comparisions being viewed when the comparison is finished comparing ([8aaff20](https://github.com/Reaparr/Reaparr/commit/8aaff200dda443f641068c5a902052a68c6257ec))


### Performance Improvements

* **WebAPI:** Batch media cache invalidations ([174a67b](https://github.com/Reaparr/Reaparr/commit/174a67b23309bdd7aeb6d4d0c2d3591cc03fcc12))
* **WebAPI:** Implemented performance tuning for Sqlite database to increase performance ([c888965](https://github.com/Reaparr/Reaparr/commit/c8889655b051474c2a904d37dcbc58696434389e))
* **WebAPI:** Only load what is needed to make an incremental Plex library sync ([8d15e62](https://github.com/Reaparr/Reaparr/commit/8d15e628ec25c50af04c023d6e578f72ac8c3dff))
* **WebAPI:** Optimize database queries for comparison state commands ([73607c3](https://github.com/Reaparr/Reaparr/commit/73607c3226e34006d17c40d86b2fe50f152ae07d))
* **WebAPI:** Optimized LibraryComparisonJob to be less DB heavy ([f48973a](https://github.com/Reaparr/Reaparr/commit/f48973ad7097b69d6bca11ae983bc5e836532d5f))
* **WebAPI:** Remove redundant indexes ([6e19006](https://github.com/Reaparr/Reaparr/commit/6e19006e86a70b4ef361a44a7b60c662314898a2))
* **Web-UI:** Resolved an perf issue with translations being deeply reactive ([60fb174](https://github.com/Reaparr/Reaparr/commit/60fb1746a131e755dcca524b74e215b210625142))

# [0.39.0-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.39.0-dev.1...v0.39.0-dev.2) (2026-08-26)


### Bug Fixes

* **WebAPI:** Add UMASK environment variable for group-writable files ([6d43c92](https://github.com/Reaparr/Reaparr/commit/6d43c9246fd5c1e09c4d5581f16675592001b996))
* **Web-UI:** Adjust column widths in downloads table ([41c1a9d](https://github.com/Reaparr/Reaparr/commit/41c1a9d405b09fee8d73b9aeac2faf27c33b288c))
* **Web-UI:** Disable cancel button when deleting a server ([9a027e3](https://github.com/Reaparr/Reaparr/commit/9a027e30e80d27cc0a7c95e35386318cfdc53f4b))
* **WebAPI:** Downloading from a server which has not auth token anymo… ([#649](https://github.com/Reaparr/Reaparr/issues/649)) ([5b7becd](https://github.com/Reaparr/Reaparr/commit/5b7becdf25a5058366d6737ff24a3b53db6ba501))
* **WebAPI:** Downloading from a server which has not auth token anymore will now correctly make as AuthError ([6519804](https://github.com/Reaparr/Reaparr/commit/6519804a9c7651281aeb7d6be8abfadac60cc500))
* **Web-UI:** Enhance download notification translations for TV shows, seasons, and episodes ([3deb5a7](https://github.com/Reaparr/Reaparr/commit/3deb5a788d0db93fc42453e1a23d28b45f669fc9))
* **WebAPI:** Enhance download task retry logic and status handling ([981c712](https://github.com/Reaparr/Reaparr/commit/981c712ea2759866a3643bdf46922b94f2344695))
* **WebAPI:** Enhance media query cache initialization before background jobs ([d6bd17a](https://github.com/Reaparr/Reaparr/commit/d6bd17af40f5883eb290611fca6f7beddcf443a2))
* **WebAPI:** Fix the missing ETA when downloaded files are moved ([49d432f](https://github.com/Reaparr/Reaparr/commit/49d432f76b9b9ae1bd8199a65c3f1958a4065ceb))
* **Web-UI:** Fixed the clear completion dialog clearing the wrong Download table of completed download tasks ([c198a5c](https://github.com/Reaparr/Reaparr/commit/c198a5c33bbd356ab679c17c7db4b2cc8f195018))
* **Web-UI:** Fixed the media overview not returning to the last scroll position ([793bb4a](https://github.com/Reaparr/Reaparr/commit/793bb4ad6c48b533af7dcf90a269b9bcf68b82da))
* **Web-UI:** Normalize scrollbar styling ([d03a2af](https://github.com/Reaparr/Reaparr/commit/d03a2afa8f0ec11e5de4ce99c9572dbd4e740bb1))
* **WebAPI:** Optimize Plex server deletion logic ([f501fb4](https://github.com/Reaparr/Reaparr/commit/f501fb47e1d0bcf54bd455baeda6e2b742cc42c9))
* **WebAPI:** Re-implement retry logic for download tasks especially when the connection is poor ([646b805](https://github.com/Reaparr/Reaparr/commit/646b805433607545f52832b3c4b885861b845b05))
* **Web-UI:** Restore server access refresh flow ([f30cd32](https://github.com/Reaparr/Reaparr/commit/f30cd3279b448360dc0b56e2bd41c0afa8fd8f59))
* **WebAPI:** Restore transient download retries ([6c2c844](https://github.com/Reaparr/Reaparr/commit/6c2c8448f222f4a6f260c353ff277d2a09e39e3c))
* **WebAPI:** Update time remaining calculation for moving download tasks ([ca02598](https://github.com/Reaparr/Reaparr/commit/ca02598a79a0edd7c0c278fa6e82ee57d26666ad))


### Features

* **WebAPI:** Add seperate refresh access endpoint for Plex server accounts ([3f1fc47](https://github.com/Reaparr/Reaparr/commit/3f1fc4777dfa28a8a9a343526f33290df77f0f53))
* **Web-UI:** Add the selected destination in the download confirmation dialog next to the download button ([ef7cea5](https://github.com/Reaparr/Reaparr/commit/ef7cea53d1877491fecbbc1010ddf99674610324))
* **Web-UI:** Media highlight the last viewed media when navigating ([a1af839](https://github.com/Reaparr/Reaparr/commit/a1af83969e27627ba58a7a4d2096952c04ecf7db))

# [0.39.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.38.3...v0.39.0-dev.1) (2026-08-19)


### Bug Fixes

* **WebAPI:** Align ToParentState Missing/Partial semantics and test matching ([795a281](https://github.com/Reaparr/Reaparr/commit/795a2810ab56ab8ea5af946eec4e57f23f278e8c))
* apply CodeRabbit auto-fixes ([3bf6754](https://github.com/Reaparr/Reaparr/commit/3bf67549430e52c7f0d81dbefff2290013589457))
* **WebAPI:** Auto cancel a Plex Library when the access token is invalidated mid-run ([c418332](https://github.com/Reaparr/Reaparr/commit/c4183329ac9bbbbe086bf89cb636e73b371f6450))
* **WebAPI:** avoid duplicate queued jobs ([948794f](https://github.com/Reaparr/Reaparr/commit/948794f27dfe05f2e8c9fec6ae4becf44b132dc3))
* **Web-UI:** Correct store import usage ([f1bc11e](https://github.com/Reaparr/Reaparr/commit/f1bc11edd99170258e30d8a2d1dc950b03067f73))
* **WebAPI:** Correct TV comparison state filtering ([a9e547d](https://github.com/Reaparr/Reaparr/commit/a9e547dfeef2eea3ccdbf2ea39da59f1062df070))
* **WebAPI:** Deduplicate Plex library refresh rows ([d38720d](https://github.com/Reaparr/Reaparr/commit/d38720d6c6d058be070afc0fb9a08dd7f22b8f28))
* **Web-UI:** Display ETA in library media sync dialog ([0f0e163](https://github.com/Reaparr/Reaparr/commit/0f0e16356eacd744519b7055ff405cfb8cd76d84))
* **WebAPI:** Exclude disabled libraries from comparison queueing in SetServerOwned ([9b492c7](https://github.com/Reaparr/Reaparr/commit/9b492c70779aa5e692dbe672608e378a3b3260a4))
* **WebAPI:** Fix concurrency group to prevent reusable workflow job cancellation ([9b12d93](https://github.com/Reaparr/Reaparr/commit/9b12d934de5bea62523791b10ef08bf197ae688e))
* **WebAPI:** Fix exception: resume only when the Direct Download snapshot contains at least one chunk ([051cd2f](https://github.com/Reaparr/Reaparr/commit/051cd2f014c05e4788d78f9ee793b00dc8d3c537))
* **Web-UI:** Fix poster hover overlay for items with missing thumbnails ([cce751b](https://github.com/Reaparr/Reaparr/commit/cce751badc02995d470d4518cc04e16ba12befa1))
* **WebAPI:** Fix stopping scheduler first ([bfab6d0](https://github.com/Reaparr/Reaparr/commit/bfab6d01e9a18acf24a80a8bead44322f2474c29))
* **Web-UI:** Fix the download confirmation destination menu not working when there are no custom paths ([6233562](https://github.com/Reaparr/Reaparr/commit/62335624099ade7df12a22d31c339f33c3ff3fe1))
* **Web-UI:** Fix the flashing of not synced media in media overview while it was still loading ([ab0bbca](https://github.com/Reaparr/Reaparr/commit/ab0bbcaef933fe0955d5667a15b27418fa5090f7))
* **WebAPI:** Fixed an exceptions when a disabled Plex library is re-synced ([13c08ad](https://github.com/Reaparr/Reaparr/commit/13c08ad22b43a4d7ea6215f523c6a3d03fa7b0d5))
* **WebAPI:** Fixed an Plex server returning an unauthorized response when loading thumbnails ([0974078](https://github.com/Reaparr/Reaparr/commit/09740789bb7c12287e87207cad5e6cd5621bc34c))
* **WebAPI:** Fixed incorrect default Plex library destination assinged to Music and Photo's libraries ([432b3aa](https://github.com/Reaparr/Reaparr/commit/432b3aaabad5eb215296db3d81826e7fb48ff693))
* **Web-UI:** Fixed missing progress when the CheckAllConnectionsStatusByPlexServerJob runs in the dialog ([fc79563](https://github.com/Reaparr/Reaparr/commit/fc7956331d8b127fcf6974a47e2b16df20134d57))
* **Web-UI:** Fixed the issue where all Plex servers were shown when re-syncing the Plex Libraries from the Plex Server settings ([cba0f76](https://github.com/Reaparr/Reaparr/commit/cba0f76b61ea6d63b3a9e091b88cf86ccb43a7bf))
* **WebAPI:** Fixed the missing quality options in the media filter ([89aed96](https://github.com/Reaparr/Reaparr/commit/89aed9616dafb55b530ddc94718d4d821b4d0f9f))
* **Web-UI:** Fixed the overflow of long titles which now dynamicy sizes the title based on its length in media posters ([10370b2](https://github.com/Reaparr/Reaparr/commit/10370b2d4e228ca506ed1aa31a025679083673ed))
* **Web-UI:** Fixed the Plex account refresh menu showing all accounts as loading when only 1 account has been requested to be refreshed ([56e008d](https://github.com/Reaparr/Reaparr/commit/56e008d2a1b40408fd23bf14c001a08f9b9dd969))
* **WebAPI:** Fixed tv-show/season download task not showing the total ETA ([2dc326a](https://github.com/Reaparr/Reaparr/commit/2dc326a52f4febb310fd30a8d9082218d5fb02ea))
* **WebAPI:** Flatten comparison detail episodes ([4440133](https://github.com/Reaparr/Reaparr/commit/444013385cb934dff11b23b09c8779c4c78d0956))
* **WebAPI:** Handle empty thumbnail responses ([5fb8557](https://github.com/Reaparr/Reaparr/commit/5fb85577426fa79e13c8d1978f5cdcbca85de6d4))
* **WebAPI:** Handle LibrarySync job cancellation when state changes during deletion ([ad54f0d](https://github.com/Reaparr/Reaparr/commit/ad54f0d0e4005a70f18d64b0906f3651022f9dd6))
* **WebAPI:** Improve cancellation handling in download tasks ([d1c2be9](https://github.com/Reaparr/Reaparr/commit/d1c2be971bb2110d7ae8025421c898b5a4d239da))
* **WebAPI:** Increase database retry interval for Quartz jobs ([226485c](https://github.com/Reaparr/Reaparr/commit/226485c50bca40aba7a75b14120f649350a1e44c))
* **background-jobs:** isolate TickerQ execution context ([28c0fc8](https://github.com/Reaparr/Reaparr/commit/28c0fc8ac2e0be0debc8e2a85bed5c09fdb0e88b))
* **WebAPI:** Mark library sync as completed before continiung with comparision job ([c8d794c](https://github.com/Reaparr/Reaparr/commit/c8d794cf5f205f19b86368fc985e1ebddf4b3dce))
* **Web-UI:** Only show relevant library sync jobs in the SyncServerMediaDialog.vue when opened ([4222ed1](https://github.com/Reaparr/Reaparr/commit/4222ed1eb28fcb6bcdb6e5fca67e08fc7a035437))
* **WebAPI:** Only use Plex accounts that are still valid ([e297942](https://github.com/Reaparr/Reaparr/commit/e297942cd02d52662ea68b38f12bbadc91edd353))
* **WebAPI:** persist Plex token validation state ([e00bfce](https://github.com/Reaparr/Reaparr/commit/e00bfce566b812b869afeab256da4738d226ed54))
* preserve reparented TV hierarchy during sync ([b949f3c](https://github.com/Reaparr/Reaparr/commit/b949f3cca2e20233cb79cc9348ce2548630e3e48))
* **WebAPI:** Prevent duplicate library comparison job triggers ([588a1bc](https://github.com/Reaparr/Reaparr/commit/588a1bc0d1fe28615b474428f7957f3f2f188c62))
* **WebAPI:** Prevent duplicate library sync execution ([d152b8c](https://github.com/Reaparr/Reaparr/commit/d152b8c37d4876ec991b3233806d86091d7cb218))
* **Web-UI:** Prevent empty media flash during filter refresh ([721a761](https://github.com/Reaparr/Reaparr/commit/721a7614ba0efff87ce27b63e770379c7365baeb))
* **WebAPI:** Project TV show detail comparison state ([3320857](https://github.com/Reaparr/Reaparr/commit/3320857afa3f0dcbb27c2cea48800565dbae1a09))
* **WebAPI:** queue sync for changed plex libraries ([1b89ea0](https://github.com/Reaparr/Reaparr/commit/1b89ea097f2b45cecfa40c43558eff5c19cd90cf))
* **WebAPI:** Recompute TV library sync metrics ([ce50624](https://github.com/Reaparr/Reaparr/commit/ce50624814314377942e43414b31dc1542343d54))
* **WebAPI:** Remove concurrency block to fix reusable workflow job cancellation ([9d27992](https://github.com/Reaparr/Reaparr/commit/9d279922f9dcc122182ab1f1f6fd90d0667a08c5))
* **WebAPI:** Remove library access for revoked servers ([94308a6](https://github.com/Reaparr/Reaparr/commit/94308a67475a81673d671f79a3128398026bcdca))
* **Web-UI:** Requeue enabled unsynced libraries ([36d7da1](https://github.com/Reaparr/Reaparr/commit/36d7da14c2ac443a15afa2f38cbfcf8de80bb926))
* **WebAPI:** Requeue enabled unsynced libraries ([c213405](https://github.com/Reaparr/Reaparr/commit/c21340572b113a24daaad26c4148e431b3ebde20))
* **WebAPI:** Require requests for queued background jobs ([ae0d9e3](https://github.com/Reaparr/Reaparr/commit/ae0d9e3757ecd1ed0a179e92dfa9d01524af76df))
* **Web-UI:** Reset media navigation indexes on query change ([ed0f5c5](https://github.com/Reaparr/Reaparr/commit/ed0f5c5eec8a01b29dd0843f8bf61967650c23e3))
* resolve cancellation pipeline failures ([852bcc6](https://github.com/Reaparr/Reaparr/commit/852bcc6f100e9d84656a48fdb33abe67e62c019b))
* **data:** return results from serialized transactions ([22a6669](https://github.com/Reaparr/Reaparr/commit/22a66696b289ba7f8af8f3cbf96348e0d951bcfa))
* **WebAPI:** Show pending comparison state while queued ([c2d6b51](https://github.com/Reaparr/Reaparr/commit/c2d6b519da4cc2e7949e7185796b41071e341553))
* **Web-UI:** Stabilize comparison details dialog rows ([c54bb07](https://github.com/Reaparr/Reaparr/commit/c54bb07550bf81da28bbcc1dd2b0fb5513a9e86e))
* **WebAPI:** Support shared in-memory SQLite databases ([607bce1](https://github.com/Reaparr/Reaparr/commit/607bce1b2b7c3d60644be4d7a5f91ba70f4f692f))
* **WebAPI:** Update cancellation handling and improve unit tests for download tasks recovery ([d6f0401](https://github.com/Reaparr/Reaparr/commit/d6f0401f0f1bc4bbb269affe8fc7778ea90d9e77))


### Features

* **WebAPI:** Add comparison state to TV show detail endpoint ([b45d315](https://github.com/Reaparr/Reaparr/commit/b45d3154dd0d8a5be1860bf2f2af295cf36cbcec))
* **WebAPI:** Add ComparisonState filter for library comparison ([8aafbc6](https://github.com/Reaparr/Reaparr/commit/8aafbc6db1bd3fe7a1edd04d3588b2f5655638c7))
* **WebAPI:** Add library enable/disable support ([52a38b4](https://github.com/Reaparr/Reaparr/commit/52a38b48cf6487eda1a944ee8887c8cdc596ce0e))
* **Web-UI:** Add media refresh mode dialog ([b456f5b](https://github.com/Reaparr/Reaparr/commit/b456f5b6b2d1eb5539863d0bb7e00380f6412d2e))
* **Web-UI:** Add plex media comparison dialog to showcase what and how you can upgrade a media item ([f184617](https://github.com/Reaparr/Reaparr/commit/f1846177a8c3094cca08bb3625524d752dcda46d))
* **Web-UI:** Added a banner when a library has lost access ([5ca5018](https://github.com/Reaparr/Reaparr/commit/5ca50184e8aa6578d3b0acabfcaabf959f3c4171))
* **Web-UI:** Added enable/disable settings for Plex libraries in the server settings ([c25d8c1](https://github.com/Reaparr/Reaparr/commit/c25d8c12d25fe1ce47fb332fb7b015247947aaff))
* **Web-UI:** Added media comparison column to the TvShow detail screen ([02cd546](https://github.com/Reaparr/Reaparr/commit/02cd546658b1af5a83a35f5170afe834bb82136e))
* **Web-UI:** Added media comparison dialog showing what you can upgrade or is missing from your own tv-shows ([1090b1c](https://github.com/Reaparr/Reaparr/commit/1090b1ca581dc1165c1fcae8c4001d267f22c3ad))
* **Web-UI:** Added media comparison filter ([a0a97dc](https://github.com/Reaparr/Reaparr/commit/a0a97dcbda82f0ffdfcad2cb7ee0170048cccb81))
* **Web-UI:** Added movie detail screen so you can see details about the movie before downloading ([1ce622d](https://github.com/Reaparr/Reaparr/commit/1ce622dbc669d1265fee1cdf1b896e7a8848e06b))
* **WebAPI:** Added refresh Plex Account job that will run every 6 hours to check if the Plex account is still valid ([4e96e09](https://github.com/Reaparr/Reaparr/commit/4e96e09d2f2261a655a6b71be961ac47a70e753c))
* **WebAPI:** Library media sync will now sync incrementally and upda… ([#643](https://github.com/Reaparr/Reaparr/issues/643)) ([6660c09](https://github.com/Reaparr/Reaparr/commit/6660c0957ad0000ed89158550a718b68d4a8c48a))
* **WebAPI:** Library media sync will now sync incrementally and update the database only with what has changed by default. This should save SSD writes ([b8827c1](https://github.com/Reaparr/Reaparr/commit/b8827c18a21ee3874a89359c055ac78d66040af2))
* **WebAPI:** Send notification to front-end when a Plex library is done with comparing such that the front-end can refresh its data ([068a8dd](https://github.com/Reaparr/Reaparr/commit/068a8dd3ca3bac7a1775234cc29342f8bc8abce4))
* **Web-UI:** Show a yellow alert on Plex library media overview which access has been lost/revoked ([1099677](https://github.com/Reaparr/Reaparr/commit/10996773c92e6f45179560738124cc145ba36232))
* **Web-UI:** Will auto-load the current library comparisions being viewed when the comparison is finished comparing ([8aaff20](https://github.com/Reaparr/Reaparr/commit/8aaff200dda443f641068c5a902052a68c6257ec))


### Performance Improvements

* **WebAPI:** Batch media cache invalidations ([174a67b](https://github.com/Reaparr/Reaparr/commit/174a67b23309bdd7aeb6d4d0c2d3591cc03fcc12))
* **WebAPI:** Implemented performance tuning for Sqlite database to increase performance ([c888965](https://github.com/Reaparr/Reaparr/commit/c8889655b051474c2a904d37dcbc58696434389e))
* **WebAPI:** Only load what is needed to make an incremental Plex library sync ([8d15e62](https://github.com/Reaparr/Reaparr/commit/8d15e628ec25c50af04c023d6e578f72ac8c3dff))
* **WebAPI:** Optimize database queries for comparison state commands ([73607c3](https://github.com/Reaparr/Reaparr/commit/73607c3226e34006d17c40d86b2fe50f152ae07d))
* **WebAPI:** Optimized LibraryComparisonJob to be less DB heavy ([f48973a](https://github.com/Reaparr/Reaparr/commit/f48973ad7097b69d6bca11ae983bc5e836532d5f))
* **WebAPI:** Remove redundant indexes ([6e19006](https://github.com/Reaparr/Reaparr/commit/6e19006e86a70b4ef361a44a7b60c662314898a2))
* **Web-UI:** Resolved an perf issue with translations being deeply reactive ([60fb174](https://github.com/Reaparr/Reaparr/commit/60fb1746a131e755dcca524b74e215b210625142))

## [0.38.3](https://github.com/Reaparr/Reaparr/compare/v0.38.2...v0.38.3) (2026-07-20)


### Bug Fixes

* **WebAPI:** Add correct DbMetadata to QuartzSqliteConnectionProvider ([b553ed9](https://github.com/Reaparr/Reaparr/commit/b553ed91e83a9df50f21b814f4b2b23f90461d7b))
* **WebAPI:** Apply SQLite WAL and busy_timeout PRAGMAs to Quartz ADO.… ([#620](https://github.com/Reaparr/Reaparr/issues/620)) ([fcebb9a](https://github.com/Reaparr/Reaparr/commit/fcebb9acbbe7f98063b4a3c690bae7a65e039f51))
* **WebAPI:** Apply SQLite WAL and busy_timeout PRAGMAs to Quartz ADO.NET connections ([35e087e](https://github.com/Reaparr/Reaparr/commit/35e087e4d6fc4337ee570df6e38a97b5afbe01d0))
* **Web-UI:** Fix 2FA verification code pop-up not showing and instead telling the user their credentials are invalid ([a811e1d](https://github.com/Reaparr/Reaparr/commit/a811e1d40a3e4ed63086f2953cf05b058953ad6f))
* **WebAPI:** Fix connections not completing when they time-out ([68f91c7](https://github.com/Reaparr/Reaparr/commit/68f91c74b7b65001797f5e8d8d21331985d48844))
* **Web-UI:** Fix the Plex server connectable count, it will now show a Plex server finished when connectable or all fail ([1b27097](https://github.com/Reaparr/Reaparr/commit/1b27097a2945b10cc0c1d1cdb950ca185594e5d1))
* **Web-UI:** Fixed the invalid verification code input for 2FA not showing the validity ([7d80657](https://github.com/Reaparr/Reaparr/commit/7d80657da217b3326a1252443d66026323b3752f))
* **WebAPI:** Prevent metadata insert races ([f1d0b4e](https://github.com/Reaparr/Reaparr/commit/f1d0b4e945ba186d23f498504185edfbba573cd0))
* **WebAPI:** Use DbContext factory for Plex inspection ([0bb1c87](https://github.com/Reaparr/Reaparr/commit/0bb1c871d96ed743a853123397d321961ef898a2))

## [0.38.3-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.38.2...v0.38.3-dev.1) (2026-07-20)


### Bug Fixes

* **WebAPI:** Add correct DbMetadata to QuartzSqliteConnectionProvider ([b553ed9](https://github.com/Reaparr/Reaparr/commit/b553ed91e83a9df50f21b814f4b2b23f90461d7b))
* **WebAPI:** Apply SQLite WAL and busy_timeout PRAGMAs to Quartz ADO.… ([#620](https://github.com/Reaparr/Reaparr/issues/620)) ([fcebb9a](https://github.com/Reaparr/Reaparr/commit/fcebb9acbbe7f98063b4a3c690bae7a65e039f51))
* **WebAPI:** Apply SQLite WAL and busy_timeout PRAGMAs to Quartz ADO.NET connections ([35e087e](https://github.com/Reaparr/Reaparr/commit/35e087e4d6fc4337ee570df6e38a97b5afbe01d0))
* **Web-UI:** Fix 2FA verification code pop-up not showing and instead telling the user their credentials are invalid ([a811e1d](https://github.com/Reaparr/Reaparr/commit/a811e1d40a3e4ed63086f2953cf05b058953ad6f))
* **WebAPI:** Fix connections not completing when they time-out ([68f91c7](https://github.com/Reaparr/Reaparr/commit/68f91c74b7b65001797f5e8d8d21331985d48844))
* **Web-UI:** Fix the Plex server connectable count, it will now show a Plex server finished when connectable or all fail ([1b27097](https://github.com/Reaparr/Reaparr/commit/1b27097a2945b10cc0c1d1cdb950ca185594e5d1))
* **Web-UI:** Fixed the invalid verification code input for 2FA not showing the validity ([7d80657](https://github.com/Reaparr/Reaparr/commit/7d80657da217b3326a1252443d66026323b3752f))
* **WebAPI:** Prevent metadata insert races ([f1d0b4e](https://github.com/Reaparr/Reaparr/commit/f1d0b4e945ba186d23f498504185edfbba573cd0))
* **WebAPI:** Use DbContext factory for Plex inspection ([0bb1c87](https://github.com/Reaparr/Reaparr/commit/0bb1c871d96ed743a853123397d321961ef898a2))

## [0.38.2](https://github.com/Reaparr/Reaparr/compare/v0.38.1...v0.38.2) (2026-07-19)


### Bug Fixes

* **WebAPI:** Added resillience to the save changes to auto retry when getting busy signals ([bf9e467](https://github.com/Reaparr/Reaparr/commit/bf9e467dcaf5f11116442dd7264179eefe1dd55e))
* **WebAPI:** Fixed TvShow, Season and Episode not responding to Download task commands ([2c06a39](https://github.com/Reaparr/Reaparr/commit/2c06a39390c6f38c72792e471ad36b4d6480ed68))
* **WebAPI:** Handle DbUpdateException wrapping a SQLite busy error in retry loops ([4c346da](https://github.com/Reaparr/Reaparr/commit/4c346da13303205354d795e76a9e6f3f2e019eca))
* **WebAPI:** Implemented EntityFrameworkCore.Sqlite.Concurrency to resolve sqlite busy errors ([dfb5d5c](https://github.com/Reaparr/Reaparr/commit/dfb5d5ce1439009d7f458a1307d097619c485c4d))
* **Web-UI:** Open directory browser when adding folder paths ([ea7e5a1](https://github.com/Reaparr/Reaparr/commit/ea7e5a10d424502815599e932983468808b36403))
* **WebAPI:** Prevent infinite recursion when MediaQueryCache snapshot build fails ([9e9af05](https://github.com/Reaparr/Reaparr/commit/9e9af05c3ae6b55c5947605591402071a270c42f))
* **Web-UI:** Replace direct clipboard API with VueUse clipboard for l… ([#615](https://github.com/Reaparr/Reaparr/issues/615)) ([c2442e7](https://github.com/Reaparr/Reaparr/commit/c2442e7100d161d7d63d83441aaef642c3caa91a))
* **Web-UI:** Replace direct clipboard API with VueUse clipboard for log viewer ([ef2bc52](https://github.com/Reaparr/Reaparr/commit/ef2bc5283b26af8e7ce851ace05a3e13591dde8c))
* **WebAPI:** Resolve sqlite vulnerability warning ([fe45386](https://github.com/Reaparr/Reaparr/commit/fe4538619b1b82f7066dc8e56b3bf9256558cb36))
* **WebAPI:** Throttle Plex library sync scheduling and prevent multiple libraries from syncing in the same Plex server at the same time ([647c6e4](https://github.com/Reaparr/Reaparr/commit/647c6e487fc0e450efd4f045a5a436c1a49653ca))


### Performance Improvements

* **WebAPI:** Increase SQLite busy_timeout to 120s and max retries to 8 ([d6404ff](https://github.com/Reaparr/Reaparr/commit/d6404ffe344e182725f6770b2377871fe46c1422))

## [0.38.2-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.38.2-dev.1...v0.38.2-dev.2) (2026-07-19)


### Bug Fixes

* **WebAPI:** Fixed TvShow, Season and Episode not responding to Download task commands ([2c06a39](https://github.com/Reaparr/Reaparr/commit/2c06a39390c6f38c72792e471ad36b4d6480ed68))
* **WebAPI:** Implemented EntityFrameworkCore.Sqlite.Concurrency to resolve sqlite busy errors ([dfb5d5c](https://github.com/Reaparr/Reaparr/commit/dfb5d5ce1439009d7f458a1307d097619c485c4d))
* **Web-UI:** Replace direct clipboard API with VueUse clipboard for l… ([#615](https://github.com/Reaparr/Reaparr/issues/615)) ([c2442e7](https://github.com/Reaparr/Reaparr/commit/c2442e7100d161d7d63d83441aaef642c3caa91a))
* **Web-UI:** Replace direct clipboard API with VueUse clipboard for log viewer ([ef2bc52](https://github.com/Reaparr/Reaparr/commit/ef2bc5283b26af8e7ce851ace05a3e13591dde8c))
* **WebAPI:** Resolve sqlite vulnerability warning ([fe45386](https://github.com/Reaparr/Reaparr/commit/fe4538619b1b82f7066dc8e56b3bf9256558cb36))
* **WebAPI:** Throttle Plex library sync scheduling and prevent multiple libraries from syncing in the same Plex server at the same time ([647c6e4](https://github.com/Reaparr/Reaparr/commit/647c6e487fc0e450efd4f045a5a436c1a49653ca))

## [0.38.2-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.38.1...v0.38.2-dev.1) (2026-07-16)


### Bug Fixes

* **WebAPI:** Added resillience to the save changes to auto retry when getting busy signals ([bf9e467](https://github.com/Reaparr/Reaparr/commit/bf9e467dcaf5f11116442dd7264179eefe1dd55e))
* **WebAPI:** Handle DbUpdateException wrapping a SQLite busy error in retry loops ([4c346da](https://github.com/Reaparr/Reaparr/commit/4c346da13303205354d795e76a9e6f3f2e019eca))
* **Web-UI:** Open directory browser when adding folder paths ([ea7e5a1](https://github.com/Reaparr/Reaparr/commit/ea7e5a10d424502815599e932983468808b36403))
* **WebAPI:** Prevent infinite recursion when MediaQueryCache snapshot build fails ([9e9af05](https://github.com/Reaparr/Reaparr/commit/9e9af05c3ae6b55c5947605591402071a270c42f))


### Performance Improvements

* **WebAPI:** Increase SQLite busy_timeout to 120s and max retries to 8 ([d6404ff](https://github.com/Reaparr/Reaparr/commit/d6404ffe344e182725f6770b2377871fe46c1422))

## [0.38.1](https://github.com/Reaparr/Reaparr/compare/v0.38.0...v0.38.1) (2026-07-11)


### Bug Fixes

* **WebAPI:** Bypass media cache for single-library queries, use only for All view ([18558b0](https://github.com/Reaparr/Reaparr/commit/18558b0b9c0a728d03844db3c9c45598b6b6cdf1))
* **WebAPI:** Cap download memory buffer at 50MB to prevent OOM on large files. Should prevent memory issues ([e9264fe](https://github.com/Reaparr/Reaparr/commit/e9264fe24f2dee878ca0fb3027403a1d13906eb6))
* **downloads:** detect storage errors instead of generic DownloadClientError ([b9a280b](https://github.com/Reaparr/Reaparr/commit/b9a280b86dfa4a333ab6cd4235f0cc31ea6f835e)), closes [#609](https://github.com/Reaparr/Reaparr/issues/609)
* **Web-UI:** Fix cache retry countdown loop and error visibility ([253ab18](https://github.com/Reaparr/Reaparr/commit/253ab18c7eae9c533a6c1b77110d02ec99532a3f))
* **Web-UI:** Keep loading spinner during library refresh until data arrives ([545bfd4](https://github.com/Reaparr/Reaparr/commit/545bfd441b66e65d2549b2d9198dbc8a5d755138))
* **cache:** prevent stale snapshot builds from clearing dirty flag ([0cab8e6](https://github.com/Reaparr/Reaparr/commit/0cab8e6d0d000a40d15d0742a97feda42dbfb0c7))
* **WebAPI:** Replace cache-doom-loop with stale-while-revalidate and suppress invalidation during sync storms ([e7ccae1](https://github.com/Reaparr/Reaparr/commit/e7ccae19bc54bd0d19cc88eca32d63070935048c))
* **file-system:** resolve UNC path rejection when checking available disk space ([ef80c6d](https://github.com/Reaparr/Reaparr/commit/ef80c6dac98cc38b83c964692eff423e77187b78)), closes [#609](https://github.com/Reaparr/Reaparr/issues/609)
* **Web-UI:** Set loading in $reset to prevent empty-state flash on library switch ([2e96b1b](https://github.com/Reaparr/Reaparr/commit/2e96b1b682b6a4715398176323cf34835111ce42))
* **Web-UI:** Set loading state immediately on library init to prevent empty-state flash ([d319ee0](https://github.com/Reaparr/Reaparr/commit/d319ee0aa4f7fc33bea4e2ad674c0970131e9554))
* **Web-UI:** Show server error state and auto-retry on 503 cache warmup with countdown ([a8bf195](https://github.com/Reaparr/Reaparr/commit/a8bf195448ae39f605a79fd1c264550bca5b766b))
* **logging:** strip ANSI escape codes from console output when redirected ([1b34d1d](https://github.com/Reaparr/Reaparr/commit/1b34d1d24868cead0e090b41b6e4028c04df30f7)), closes [#609](https://github.com/Reaparr/Reaparr/issues/609)

## [0.38.1-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.38.0...v0.38.1-dev.1) (2026-07-11)


### Bug Fixes

* **WebAPI:** Bypass media cache for single-library queries, use only for All view ([18558b0](https://github.com/Reaparr/Reaparr/commit/18558b0b9c0a728d03844db3c9c45598b6b6cdf1))
* **WebAPI:** Cap download memory buffer at 50MB to prevent OOM on large files. Should prevent memory issues ([e9264fe](https://github.com/Reaparr/Reaparr/commit/e9264fe24f2dee878ca0fb3027403a1d13906eb6))
* **downloads:** detect storage errors instead of generic DownloadClientError ([b9a280b](https://github.com/Reaparr/Reaparr/commit/b9a280b86dfa4a333ab6cd4235f0cc31ea6f835e)), closes [#609](https://github.com/Reaparr/Reaparr/issues/609)
* **Web-UI:** Fix cache retry countdown loop and error visibility ([253ab18](https://github.com/Reaparr/Reaparr/commit/253ab18c7eae9c533a6c1b77110d02ec99532a3f))
* **Web-UI:** Keep loading spinner during library refresh until data arrives ([545bfd4](https://github.com/Reaparr/Reaparr/commit/545bfd441b66e65d2549b2d9198dbc8a5d755138))
* **cache:** prevent stale snapshot builds from clearing dirty flag ([0cab8e6](https://github.com/Reaparr/Reaparr/commit/0cab8e6d0d000a40d15d0742a97feda42dbfb0c7))
* **WebAPI:** Replace cache-doom-loop with stale-while-revalidate and suppress invalidation during sync storms ([e7ccae1](https://github.com/Reaparr/Reaparr/commit/e7ccae19bc54bd0d19cc88eca32d63070935048c))
* **file-system:** resolve UNC path rejection when checking available disk space ([ef80c6d](https://github.com/Reaparr/Reaparr/commit/ef80c6dac98cc38b83c964692eff423e77187b78)), closes [#609](https://github.com/Reaparr/Reaparr/issues/609)
* **Web-UI:** Set loading in $reset to prevent empty-state flash on library switch ([2e96b1b](https://github.com/Reaparr/Reaparr/commit/2e96b1b682b6a4715398176323cf34835111ce42))
* **Web-UI:** Set loading state immediately on library init to prevent empty-state flash ([d319ee0](https://github.com/Reaparr/Reaparr/commit/d319ee0aa4f7fc33bea4e2ad674c0970131e9554))
* **Web-UI:** Show server error state and auto-retry on 503 cache warmup with countdown ([a8bf195](https://github.com/Reaparr/Reaparr/commit/a8bf195448ae39f605a79fd1c264550bca5b766b))
* **logging:** strip ANSI escape codes from console output when redirected ([1b34d1d](https://github.com/Reaparr/Reaparr/commit/1b34d1d24868cead0e090b41b6e4028c04df30f7)), closes [#609](https://github.com/Reaparr/Reaparr/issues/609)

# [0.38.0](https://github.com/Reaparr/Reaparr/compare/v0.37.0...v0.38.0) (2026-07-11)


### Bug Fixes

* **WebAPI:** Add --channel flag to vpk upload steps to prevent OS auto-detection ([7278c40](https://github.com/Reaparr/Reaparr/commit/7278c40c0c8c369c619b0881afdfadf39fa44a24))
* **WebAPI:** Add donation link to the docker logging at the top ([b116b23](https://github.com/Reaparr/Reaparr/commit/b116b23f5c205445cba891443af6f67f8dbe98de))
* **WebAPI:** Add DownloadQueue retry cooldown ([fae413d](https://github.com/Reaparr/Reaparr/commit/fae413d8e1e45f3c904c653bd5a1f0c0a3349d24))
* **WebAPI:** Add merge option to GitHub upload commands ([c17dbf0](https://github.com/Reaparr/Reaparr/commit/c17dbf02047d7773740e373abd6af864a03b5978))
* **WebAPI:** Add retry cooldown and boot-time queue kick to DownloadQueue ([#586](https://github.com/Reaparr/Reaparr/issues/586)) ([5c412ee](https://github.com/Reaparr/Reaparr/commit/5c412eed2088129201a9c5078dfdf44e1d1dca2f))
* **WebAPI:** Add retry cooldown and boot-time queue kick to DownloadQueue ([8fd13ac](https://github.com/Reaparr/Reaparr/commit/8fd13acb6974e11a12eccba910e3f88fafacde9a))
* **WebAPI:** Add retry cooldown and boot-time queue kick to DownloadQueue ([3050f52](https://github.com/Reaparr/Reaparr/commit/3050f52093d26c6b42b5b7d686c1fdeabbc23c57))
* **WebAPI:** Allow download folder media type none ([5bb25b9](https://github.com/Reaparr/Reaparr/commit/5bb25b9d1a8d993170804bb3847a158a64b092b6))
* **WebAPI:** Auto-retry downloads after ServerUnreachable, Error, DownloadClientError ([#584](https://github.com/Reaparr/Reaparr/issues/584)) ([957ef85](https://github.com/Reaparr/Reaparr/commit/957ef8519fd1a26aebf5cfae3410017da63f9413))
* **WebAPI:** Auto-retry downloads after ServerUnreachable, Error, DownloadClientError ([de67420](https://github.com/Reaparr/Reaparr/commit/de67420dd22ae78d709903e37f5b0dfde8f86e06))
* **WebAPI:** Backfill granted history events ([e767540](https://github.com/Reaparr/Reaparr/commit/e76754098a5be52ae8adece1458ba3055b372314))
* **WebAPI:** Correct all-library alphabet navigation order ([396eea4](https://github.com/Reaparr/Reaparr/commit/396eea4d92627786f3be398ea4f38b79ab9ecf16))
* **WebAPI:** Correct desktop packaging executable names ([9816443](https://github.com/Reaparr/Reaparr/commit/981644382c269370e8bf5b13db28d1ce602bf340))
* **Web-UI:** Correct docker runtime config platform and version baking ([360db11](https://github.com/Reaparr/Reaparr/commit/360db117fbd9a4210d30fb74cf4834735f7fa73e))
* **WebAPI:** Disable downloader disk precheck ([d8b0290](https://github.com/Reaparr/Reaparr/commit/d8b0290e9c5b99e8f7cfce4e63138ab58df06164))
* **WebAPI:** Disable paging for zero page size ([691524e](https://github.com/Reaparr/Reaparr/commit/691524ef58ad92e0622f617f78e9fb05d14ef7ff))
* **Web-UI:** Distinguish between disabled and inaccessible servers ([2039230](https://github.com/Reaparr/Reaparr/commit/20392309764672dcf3f944384366f05979465967))
* **Web-UI:** Enforce auth status and logout consistency ([8020869](https://github.com/Reaparr/Reaparr/commit/802086997e5fbea4029e50928c3f27d4fd6ea446))
* **WebAPI:** enforce init-adduser before reaparr service startup ([7de81cb](https://github.com/Reaparr/Reaparr/commit/7de81cb392c14e2ef1a31f0ef7ed187eacced5b0))
* **WebAPI:** Ensure desktop window is disposed and re-created after closing to background ([ed3e198](https://github.com/Reaparr/Reaparr/commit/ed3e198f085224d58513ff62c0492b2bc4cda684))
* **WebAPI:** Ensure per-runtime release channels and frontend depende… ([#572](https://github.com/Reaparr/Reaparr/issues/572)) ([7b01438](https://github.com/Reaparr/Reaparr/commit/7b014386a6139166cbb473f768efe861cb59a4b0))
* **WebAPI:** Ensure per-runtime release channels and frontend dependency install ([7db5301](https://github.com/Reaparr/Reaparr/commit/7db5301553f6ee3c6b525436d53286a47360a23c))
* **WebAPI:** Ensure Reaparr closes fully when applying the update ([2869197](https://github.com/Reaparr/Reaparr/commit/2869197a9de7f38c9b38df4941f079317990f1f2))
* **WebAPI:** Escape search query values that might mess up the search ([2757865](https://github.com/Reaparr/Reaparr/commit/27578653e9230aa1b6d3ea6ba0e0ed2a78bfbfbe))
* **WebAPI:** Finally fixed a several year old problem where Reaparr would crash the first time and then work afterwards! It now starts up much faster the first time ([f58e989](https://github.com/Reaparr/Reaparr/commit/f58e9891212337082a0669a504f693c0cf3e7e43))
* **WebAPI:** Fix a startup issue on Windows desktop where background jobs were not found ([69ee826](https://github.com/Reaparr/Reaparr/commit/69ee8262b60c077633e40e4e90d8bc9e42da6ae7))
* **WebAPI:** Fix Build unit test dependency wiring ([f0a2da0](https://github.com/Reaparr/Reaparr/commit/f0a2da068bde95bceab87eff1ba5e0b46a28a7b1))
* **WebAPI:** Fix Docker /Config ownership bootstrap for [#578](https://github.com/Reaparr/Reaparr/issues/578) ([6e491a2](https://github.com/Reaparr/Reaparr/commit/6e491a227d283c92c697c1f4eae91b5600a67e3e))
* **WebAPI:** Fix Dockerfile stage parsing ([6ae517e](https://github.com/Reaparr/Reaparr/commit/6ae517ebbbd206b02f49d84c653fc44c0e0cd893))
* **WebAPI:** Fix incorrect job status updates due to missing listeners ([344d642](https://github.com/Reaparr/Reaparr/commit/344d6425be7aee83baaf8c65ada6e28f18dda5e8))
* **WebAPI:** Fix integration test download flow ([1994ace](https://github.com/Reaparr/Reaparr/commit/1994acef27940ee6eedfe22ef451e12818612163))
* **WebAPI:** Fix missing log text in tests ([a5e18e1](https://github.com/Reaparr/Reaparr/commit/a5e18e174e69dc201d63a88bb37f15a7365887ef))
* **WebAPI:** Fix the Reaparr app not exiting on applying update ([e90ba14](https://github.com/Reaparr/Reaparr/commit/e90ba1424071f3220e7b029caaa60160e9927a1c))
* **WebAPI:** Fixed an issue where inaccessible servers would still show up on the home page, this media is now always filtered out ([b4d08a6](https://github.com/Reaparr/Reaparr/commit/b4d08a6f96ee747e269dd466a0ca3f2aac52e91b))
* **WebAPI:** Fixed an issue where the default FolderPaths could not be updated ([23030a9](https://github.com/Reaparr/Reaparr/commit/23030a9fe258c753560dacf8cd0911b776934f40))
* **WebAPI:** Fixed an issue where the logging to log files contained a lot of noise making it unreadable ([8add639](https://github.com/Reaparr/Reaparr/commit/8add639722cdb087cdbebd431f0a82449b8baac1))
* **WebAPI:** Fixed external links not opening the browser when running Reaparr in Desktop mode ([5fce646](https://github.com/Reaparr/Reaparr/commit/5fce646a1f7b961d2c94e37a3646bd63608f83f2))
* **WebAPI:** Fixed new database creation failing due to incorrect seed data ([4365743](https://github.com/Reaparr/Reaparr/commit/43657439f3e2b6657caa83f882ee65b5dc30d7fe))
* **WebAPI:** Fixed PlexLibrary not being sent after a RefreshLibraryMedia ([cb323bc](https://github.com/Reaparr/Reaparr/commit/cb323bcea6091d1d76d3154d0397d4ee80176663))
* **Web-UI:** Fixed some setup page translations not updating when the language is changes ([a7a8b37](https://github.com/Reaparr/Reaparr/commit/a7a8b37a6872f7ce69a949826686d3719123b557))
* **WebAPI:** Fixed some sorting in the media overview to be doubly sorted leading to inconsisten sorting ([ce8475b](https://github.com/Reaparr/Reaparr/commit/ce8475bb6f330b459dbd40fed07dbbe5299285ee))
* **Web-UI:** Fixed the alphabetical order of the Plex libraries in the server drawer, now its movies, then tv-shows then the rest ([e344b0e](https://github.com/Reaparr/Reaparr/commit/e344b0e2f1b95a756925b39351a3ffcd15c8ee36))
* **WebAPI:** Fixed the contentChangedAt being seconds since library createdAt and not a unix since epoch ([19f46c2](https://github.com/Reaparr/Reaparr/commit/19f46c24940aa9819a11f8183eb5f981b61921c5))
* **WebAPI:** Fixed the missing new line between logging ([1ffd5ef](https://github.com/Reaparr/Reaparr/commit/1ffd5efdee7eb885b68d35fa6f542d5cb1a194a1))
* **Web-UI:** Fixed the sorting on MediaTable view ([70fb472](https://github.com/Reaparr/Reaparr/commit/70fb472697d1e7bbfa5ba2b762992bf7bd6139fa))
* **Web-UI:** Guard failed Plex account mutations ([e3bb89a](https://github.com/Reaparr/Reaparr/commit/e3bb89a6aa47f16148b12cd7260e5ec902022bd0))
* **Web-UI:** Guard failed server connection refreshes ([326d56f](https://github.com/Reaparr/Reaparr/commit/326d56f43a145e8c91691a636c362137e2cc4187))
* **WebAPI:** Handle disabled server updates ([c91c8ee](https://github.com/Reaparr/Reaparr/commit/c91c8ee767b78df0957f4b187487774fdc81bfde))
* **Web-UI:** Handle empty job update payloads ([5d89ba8](https://github.com/Reaparr/Reaparr/commit/5d89ba8201631dcbcb38b49a18a3b8bb5b896388))
* **WebAPI:** Handle HTTP retry exceptions ([e797641](https://github.com/Reaparr/Reaparr/commit/e797641b09aa7c4e9ff8c3d24807caa5da55da91))
* **WebAPI:** Handle malformed release tags and improve update check fallback ([1da7d05](https://github.com/Reaparr/Reaparr/commit/1da7d05a4e7de003987a4fba27d120ba78ef56dd))
* **WebAPI:** Handle zero media size fallback ([3f1dee5](https://github.com/Reaparr/Reaparr/commit/3f1dee59cc6b1a036626be5e004d44a3d94f4533))
* **build:** harden desktop packaging filesystem paths ([46dc9bc](https://github.com/Reaparr/Reaparr/commit/46dc9bcda8d62ee3de4fadff07a478da0590990f))
* **Web-UI:** Harden locale selection and i18n guards ([e29e72f](https://github.com/Reaparr/Reaparr/commit/e29e72fb5492b7b5b225f44dffa900cb8934be23))
* **WebAPI:** Harden move download cancellation and job queueing ([efd281f](https://github.com/Reaparr/Reaparr/commit/efd281fd07403fc1ea6254d7019e3d2fcaa7a04c))
* **WebAPI:** Log failures when notifying Radarr and Sonarr on startup ([5ad58e7](https://github.com/Reaparr/Reaparr/commit/5ad58e735558400526b283d1de446207de05723f))
* **frontend:** mark inaccessible media header names ([4feeb97](https://github.com/Reaparr/Reaparr/commit/4feeb97b06d84710e9817a05d4eb681955d69b33))
* **WebAPI:** Normalize SQLite DateTime values to UTC ([ac87642](https://github.com/Reaparr/Reaparr/commit/ac87642271170d22c18eff9fd51b0b37b4f7373e))
* **WebAPI:** Patch vulnerable ASP.NET Core packages ([f5aad38](https://github.com/Reaparr/Reaparr/commit/f5aad380886fa0d9808e1ba56d1ed5a20226bdf4))
* **WebAPI:** Persist verified download size ([166125a](https://github.com/Reaparr/Reaparr/commit/166125a8b8165c0226ba65f75b45d0c7013e4ad7))
* **Web-UI:** Preserve download command failure state ([b42d3cb](https://github.com/Reaparr/Reaparr/commit/b42d3cb112247a059d1ec854103ec3de7e0a7d22))
* **WebAPI:** Preserve Plex library metrics during refresh ([6771d1c](https://github.com/Reaparr/Reaparr/commit/6771d1c57e500b200958bab92f6254efc2385a5a))
* **WebAPI:** Queue inspect jobs with resilient disabled-server filtering ([79b769b](https://github.com/Reaparr/Reaparr/commit/79b769b68b85300f5343a5729af55478d80e7016))
* **WebAPI:** Reconcile missing direct download completion callback ([e344560](https://github.com/Reaparr/Reaparr/commit/e344560e4df7e4408ac96f43d1dc4e708078daf1))
* **WebAPI:** Recover zombie Downloading tasks left over from a crash ([#585](https://github.com/Reaparr/Reaparr/issues/585)) ([650d2aa](https://github.com/Reaparr/Reaparr/commit/650d2aa40c4b0f4c07812fceccd41dc730cb6c62))
* **WebAPI:** Recover zombie Downloading tasks left over from a crash ([35187f1](https://github.com/Reaparr/Reaparr/commit/35187f125523845c850368c5a648cbc991623869))
* **Web-UI:** Redirect to login only on unauthorized ([2d2b52e](https://github.com/Reaparr/Reaparr/commit/2d2b52eccae7399a380a4a87572092d64f74721f))
* **WebAPI:** Refresh desktop ready state per window ([f9d526c](https://github.com/Reaparr/Reaparr/commit/f9d526c8f419f763927464ae2b8023ac453bf13c))
* **Web-UI:** Refresh servers only after successful updates ([faffdd6](https://github.com/Reaparr/Reaparr/commit/faffdd63a52d060f222c671e063db342c7e9d777))
* **data:** rehydrate persisted tv show IDs before season/episode inserts ([da4e1ac](https://github.com/Reaparr/Reaparr/commit/da4e1acae9190d73898ee2063dc2f910414415a1))
* **WebAPI:** Remove Database.EnsureCreated() from DbContext constructor, this was causing errors during unit tests ([26e7d12](https://github.com/Reaparr/Reaparr/commit/26e7d12ae2cf0023e40fe375081ab394ec0597f7))
* **Web-UI:** Remove index column from mediaoverview table, its obsolete ([c2d0a52](https://github.com/Reaparr/Reaparr/commit/c2d0a523d6facec7bba9137240b06e5a1b88d4de))
* **Web-UI:** Reset integration loading state on errors ([d7f615c](https://github.com/Reaparr/Reaparr/commit/d7f615c2fc8c721c57f7adcbcca519a3c3f2a3ac))
* **WebAPI:** Respect client-owned download failure statuses ([ccf8c42](https://github.com/Reaparr/Reaparr/commit/ccf8c42a89a0e91aab97fd5ed00d9dcd6328fa86))
* **WebAPI:** Return conflict for disabled server ([184e962](https://github.com/Reaparr/Reaparr/commit/184e96219d07185bb528ae3c5eb525b379f3c567))
* **WebAPI:** Serialize desktop release uploads ([197d51e](https://github.com/Reaparr/Reaparr/commit/197d51e3f335e2038a4936bbc3fe48d516964903))
* **WebAPI:** Serialize desktop uploads via dedicated job to prevent asset conflicts ([5d8493e](https://github.com/Reaparr/Reaparr/commit/5d8493e8b5b75db30b76c84ee3b8eb47db9ed264))
* **Web-UI:** Set app version and platform when not logged in yet ([fb11391](https://github.com/Reaparr/Reaparr/commit/fb1139164029b7b710e193ccf62f447050de23ae))
* **Web-UI:** Shortend the debounce time from searching from 3 to 1 seconds to make it more reactive ([df3010e](https://github.com/Reaparr/Reaparr/commit/df3010e64b69cb38140bae9e71464964655c13e5))
* **Web-UI:** Show server name on fallback posters ([5490890](https://github.com/Reaparr/Reaparr/commit/549089046ab94c08875a3b9c8d61e6a1dee0ed14))
* **WebAPI:** Skip boot queue kick in integration tests ([57ffea1](https://github.com/Reaparr/Reaparr/commit/57ffea16e8ed22335e18feb9715c593f23b31a3c))
* **WebAPI:** Skip update check if Velopack is not installed in desktop mode ([50cdca1](https://github.com/Reaparr/Reaparr/commit/50cdca11fc58189084d7f1f0bbb42d8979e51278))
* **Web-UI:** Stabilize account dialog validation and save flows ([6d0d4a6](https://github.com/Reaparr/Reaparr/commit/6d0d4a6b35eb0fb9cdfd7c02a067686f3d02e910))
* **WebAPI:** Stabilize DownloadQueue restart integration test ([9c0d78d](https://github.com/Reaparr/Reaparr/commit/9c0d78dfcd4eb1b3eab9ce75b88f6d931099f361))
* **WebAPI:** Stabilize PublicApi torrent tests and clean test logging ([95516fc](https://github.com/Reaparr/Reaparr/commit/95516fc2dac0df357cb54fb07e968d60c55d2194))
* **Web-UI:** Stabilize settings autosave subscriptions ([e7c4a5c](https://github.com/Reaparr/Reaparr/commit/e7c4a5c057f5620d5cfa7d7dd34a8e012a83cd44))
* **Web-UI:** Stop background render loop when animation is disabled ([b521a56](https://github.com/Reaparr/Reaparr/commit/b521a566e26b99f0429622b7bd933bcee98d68ff)), closes [#573](https://github.com/Reaparr/Reaparr/issues/573)
* **Web-UI:** Surface invalid Plex credentials in account dialog ([5067b2c](https://github.com/Reaparr/Reaparr/commit/5067b2cb77fd20f754e9d817bad14e085b4b0ed9))
* **WebAPI:** Update Node.js version and improve CI workflow configurations ([285de8f](https://github.com/Reaparr/Reaparr/commit/285de8f5ff237884917a2eca9ea74b1b5211cf0c))
* **WebAPI:** Update notification messages and Node.js version in release pipeline ([9ff413d](https://github.com/Reaparr/Reaparr/commit/9ff413d2a3b224c13b53e463795a86e08fce191f))
* **WebAPI:** Use per-architecture vpk channels to prevent release asset collisions ([a072f88](https://github.com/Reaparr/Reaparr/commit/a072f883d4b7f6d6ba703cf05b91cee6b4ad8ddb))
* **WebAPI:** Use per-rid release channels for desktop uploads ([3b3122b](https://github.com/Reaparr/Reaparr/commit/3b3122b8386d9f7ef1693947e3ee3341858738ee))
* **docker:** use PUID/PGID ownership for /Config ([#579](https://github.com/Reaparr/Reaparr/issues/579)) ([f5b80ac](https://github.com/Reaparr/Reaparr/commit/f5b80acc0e53337a84b7257a2d7e535660e3ebd4))
* **docker:** use PUID/PGID ownership for /Config ([87b5308](https://github.com/Reaparr/Reaparr/commit/87b53089afa224a6a2f48a630b08717dfa73912b))
* **WebAPI:** Validate setup folder paths by existence ([5314d95](https://github.com/Reaparr/Reaparr/commit/5314d952eced13b8f685ae49283fcaef283f7449)), closes [#599](https://github.com/Reaparr/Reaparr/issues/599)
* **WebAPI:** Verify completed downloads before finishing ([#596](https://github.com/Reaparr/Reaparr/issues/596)) ([be20b8f](https://github.com/Reaparr/Reaparr/commit/be20b8f899f52fb1a166ce768bb1e1fb5e767c01))
* **WebAPI:** Verify completed downloads before finishing ([b1a72e2](https://github.com/Reaparr/Reaparr/commit/b1a72e24ca058b02e40bca3ab4b19e7bfb52c372))
* **WebAPI:** When restarting a DownloadTask, it will refetch the latest media source data ([2befea1](https://github.com/Reaparr/Reaparr/commit/2befea10b949cbeaddc25dc96248f1266e896612))


### Features

* **Web-UI:** Add app update download progress tracking in the update dialog ([c478334](https://github.com/Reaparr/Reaparr/commit/c478334d3c32c6e76abd10755c806b63abaf04ed))
* **WebAPI:** Add desktop build workflow and configuration for multiple platforms like Linux, MacOS and Windows ([e6a036d](https://github.com/Reaparr/Reaparr/commit/e6a036dbb0fa8d8205f0c6190547b6391853f7e7))
* **WebAPI:** Add development middleware to disable authentication through environment variable I_AM_DUMB_SO_DISABLE_AUTHENTICATION ([990aa33](https://github.com/Reaparr/Reaparr/commit/990aa335565e65c3659ef553cc23aad631794348))
* **WebAPI:** Add error dialog on startup if Reaparr in desktop mode fails ([9e82a78](https://github.com/Reaparr/Reaparr/commit/9e82a785ebb596f31339df794d324f1ecf1e1e95))
* **WebAPI:** Add error pop-up dialog when Reaparr fails to start in desktop mode ([d7baf59](https://github.com/Reaparr/Reaparr/commit/d7baf59b8f40ddab0e3da9603ebc21d3c0bd5121))
* **Web-UI:** Add loading indicators for filter menu and alphabet navigation ([84fb489](https://github.com/Reaparr/Reaparr/commit/84fb489ea2217844d57235665329f3ed460b2973))
* **WebAPI:** Add retry mechanism to poster image loading ensuring far less image time-outs ([3a80841](https://github.com/Reaparr/Reaparr/commit/3a80841351d47b5c00d3862238a7093b39ee1b0f))
* **WebAPI:** Add scripts and configurations for desktop builds on Linux, MacOS, and Windows ([3169fff](https://github.com/Reaparr/Reaparr/commit/3169fff04257dd84c4ca44a2a04b36ce3666c424))
* **Web-UI:** Add strikethrough through the Plex server name on the Downloads Table when there is no Plex account that has access ([570b861](https://github.com/Reaparr/Reaparr/commit/570b861881a9034b8decb3e4d6ac51fb11c9ae4e))
* **Web-UI:** Added a spinning animation next to Plex Servers that are currently syncing their Plex libaries ([36b8b1f](https://github.com/Reaparr/Reaparr/commit/36b8b1f20dd30cbc57d223ad513a028537337121))
* **Web-UI:** Added a update dialog when there is a new Reaparr release ([58da7db](https://github.com/Reaparr/Reaparr/commit/58da7dbbe6a3b9bab3bcaf6c1379fac594805b96))
* **WebAPI:** Added an override to mark a server as owned such that it will not be used in Sonarr/Radarr results. This prevents downloading your own media already in your possession ([dcbb909](https://github.com/Reaparr/Reaparr/commit/dcbb90960b82f4b9cb3ae5362f2308ad3db114b9))
* **WebAPI:** Added auto-recovery of DownloadTasks when Reaparr crashes and auto pause when downloading and Reaparr shutsdown ([b755760](https://github.com/Reaparr/Reaparr/commit/b7557606822d8c1b0d5827a1570d3f632def4385))
* **WebAPI:** Added fallback for Windows to lauch browser if the desktop window fails to render thanks to missing webview ([334a7d9](https://github.com/Reaparr/Reaparr/commit/334a7d9dfebaa556a7912177def691d1e01cff31))
* **Web-UI:** Added IsOwned and IsEnabled toggles on the Server Settings dialog ([dc70c58](https://github.com/Reaparr/Reaparr/commit/dc70c586e6c28476a03ce10f296c5f411ffa869a))
* **WebAPI:** Added option to add a GITHUB_TOKEN to Reaparr to circumvent the Github ratelimits ([15ca26a](https://github.com/Reaparr/Reaparr/commit/15ca26ae0f83b2106c61c10c87d4ac2e82e8430d))
* **WebAPI:** Added periodic auto PlexLibrary media sync, every hour Reaparr will check if there is an update to a PlexLibrary ans resync the media ([890ce26](https://github.com/Reaparr/Reaparr/commit/890ce26709fca806cae1c21643467ae0e2e14524))
* **Web-UI:** Added Plex server command button in the Server settings to delete a Plex Server ([baf277c](https://github.com/Reaparr/Reaparr/commit/baf277c3a6f8a3f29fbb0e12ec63d2c252af09ed))
* **WebAPI:** Added PlexLibrary access history tracking to determine and look back if access to a library has been lost or gained ([edfd374](https://github.com/Reaparr/Reaparr/commit/edfd374472449805e9e9ecb14e9da8fc58699380))
* **Web-UI:** Added progress to the download update pop-up ([931e110](https://github.com/Reaparr/Reaparr/commit/931e110bbc0583f87c2dca297d9d4ab9e36336e6))
* **WebAPI:** Added seperate build CLI project for the desktop build workflow ([2df35d6](https://github.com/Reaparr/Reaparr/commit/2df35d637a3d8c8465238e208a95de31f04e34dd))
* **Web-UI:** Added translations for the new AutoPaused and AutoMovePaused ([25fc94c](https://github.com/Reaparr/Reaparr/commit/25fc94c7af54f11fcf3518c541454500af9d0cd8))
* **Web-UI:** Auto scroll to the last scroll position when navigating from and to the media overview ([45afb8f](https://github.com/Reaparr/Reaparr/commit/45afb8f9e96c0800f8cb34241f7524be5dabfc3d))
* **WebAPI:** Auto-recovery for Downloadtasks that are downloading or moving media files to destination when Reaparr shuts down or crashes ([f436d1c](https://github.com/Reaparr/Reaparr/commit/f436d1cec86b32859bc3382dcb3ae1ed59a61adb))
* **Web-UI:** Enhance log viewer with selection and copy functionality ([ecd0c0d](https://github.com/Reaparr/Reaparr/commit/ecd0c0daddb19dfc3ecaabc771937f5e7640ad4e))
* **Web-UI:** Enhance log viewer with selection and copy functionality ([d26c577](https://github.com/Reaparr/Reaparr/commit/d26c577260ad5beea64415aa2c97b4f639c414d4))
* **Web-UI:** Filtering media in the MediaOverview will now only display options that will give results ([1fd0165](https://github.com/Reaparr/Reaparr/commit/1fd01654c8e51eac92533aa4f9dbf122e25f335f))
* **Web-UI:** First draft version of Plex library access history overview ([c7123ab](https://github.com/Reaparr/Reaparr/commit/c7123abed23253c6b95961ade7a1b654c8580a21))
* **WebAPI:** Implement app update download progress tracking ([aec0c6e](https://github.com/Reaparr/Reaparr/commit/aec0c6eb93b6dd3b5bca7c7eff7f2528cb30fae2))
* **WebAPI:** Implement desktop single-instance coordination and window lifecycle. This allows Reaparr to keep running in the background when the window closes on desktop ([fcbc34c](https://github.com/Reaparr/Reaparr/commit/fcbc34c1adccd969deff246356f1a082df6451b5))
* **WebAPI:** Implement GitHub releases fetching and mapping to showcase new updates ([a750151](https://github.com/Reaparr/Reaparr/commit/a750151a4923ef4a667304867de29b2090565839))
* **WebAPI:** Implement live log streaming via SignalR ([63a5492](https://github.com/Reaparr/Reaparr/commit/63a5492f9265208dccf0df4263d9de84e5fda11e))
* **Web-UI:** Implement live log viewer under Settings => Logs with realtime updates ([6bc494e](https://github.com/Reaparr/Reaparr/commit/6bc494e9e0b6f8f8dd13827ad7ff7d129b1bcef9))
* **WebAPI:** Implement update management endpoints for checking and applying updates ([0e32c45](https://github.com/Reaparr/Reaparr/commit/0e32c45673695c22162ecfe9d22adfb10e3c09fb))
* **WebAPI:** Implement update Reaparr check endpoint and related background job to keep checking every hour ([bb0ed40](https://github.com/Reaparr/Reaparr/commit/bb0ed409a3b286ecfcbb58b36ac94aa41c3cd76a))
* **WebAPI:** Implemented FlexQuery.NET as the new media filter and sorter ([e9c6da3](https://github.com/Reaparr/Reaparr/commit/e9c6da36a6601986c3aa71efe15cbed0bb128327))
* **Web-UI:** Reaparr now reloads a media overview with the last set filter and sort parameters ([ff55764](https://github.com/Reaparr/Reaparr/commit/ff55764bd5dda0dd86d37368c605f938e2afb64a))
* **Web-UI:** Removed the hide button in the Plex server settings, this is now meant to be hidden/disabled through the Server Configuration tab ([29166a8](https://github.com/Reaparr/Reaparr/commit/29166a8a07c745532465240ab52b2a6af625ed23))
* **Web-UI:** There is now a Library Access timeline visible under settings => Accounts => Scroll to bottom to view ([7728655](https://github.com/Reaparr/Reaparr/commit/7728655ba885ce6b82c2454dd9b077524df018f8))
* **Web-UI:** When clicking on a media flter menu item again, after it is checked, will now uncheck it ([c30b696](https://github.com/Reaparr/Reaparr/commit/c30b6969216272c2ca60f6d834fdd634c8a99380))


### Performance Improvements

* **WebAPI:** Added media overview cache for the home page to greatly improve loading times ([acfbbee](https://github.com/Reaparr/Reaparr/commit/acfbbee3a2078b5458e10a10071505da398ab821))
* **Web-UI:** Batch page fetches, reduce overscan, and prefetch before scroll ([c09faec](https://github.com/Reaparr/Reaparr/commit/c09faeca126234b3a8580e08de4417dc505e588f))
* **Web-UI:** Defer thumbnail loading to viewport-visible rows only ([c4d77b1](https://github.com/Reaparr/Reaparr/commit/c4d77b139ddcc941c05b75055543cf190ebc28b4))
* **Web-UI:** Fetch filter metadata via dedicated endpoint instead of page response ([e91086c](https://github.com/Reaparr/Reaparr/commit/e91086ca1baa926141da1d723c079c828d2c5417))
* **Web-UI:** Implement paginated loading for media poster pages, this should make first-load much faster ([cacdfc5](https://github.com/Reaparr/Reaparr/commit/cacdfc53abc8e548ae79865bfda1c4010f83f8af))
* **Web-UI:** Made some medium performance improvement on the poster media table to speed up rendering ([abc9ecf](https://github.com/Reaparr/Reaparr/commit/abc9ecf74a7d584c277ece071057cc0fa63a5a19))
* **Web-UI:** Made some performance improvement Mediaposter to use less nested components to speed up rendering ([f6c3896](https://github.com/Reaparr/Reaparr/commit/f6c38969f65e71b2663730ad20deb0048f68e342))
* **WebAPI:** Move media sorting calculations to back-end to increase performance in the the front-end ([b1905f8](https://github.com/Reaparr/Reaparr/commit/b1905f835bb1de436da3cdcd54840c18df240ed0))
* **Web-UI:** Optimize poster table render and scroll performance ([e0ff569](https://github.com/Reaparr/Reaparr/commit/e0ff569d11e68f68c21fcd1fb79aeda346c27365))
* **Web-UI:** Preload thumbnails for overscan rows with wider buffer ([691eafb](https://github.com/Reaparr/Reaparr/commit/691eafbf2f9408c66dc5244a62a1370d6265e0ab))
* **WebAPI:** Strip filter metadata from media page responses and serve lazily ([9ff0807](https://github.com/Reaparr/Reaparr/commit/9ff0807d48352c822de26e579057e9a4e87b41f5))


### Reverts

* **Web-UI:** Revert "improvements" ([cb481da](https://github.com/Reaparr/Reaparr/commit/cb481da31a34e222af0c69daf0fedb742b7b282f))
* **Web-UI:** Revert "improvements" ([359583a](https://github.com/Reaparr/Reaparr/commit/359583a165710fc0bfdfbe55729fac743e5202e5))
* **WebAPI:** Revert migration ([b1cb378](https://github.com/Reaparr/Reaparr/commit/b1cb378cbe5d93c77be1a438878c4225ace7f1d2))
* **WebAPI:** Revert migraton ([59d5d1f](https://github.com/Reaparr/Reaparr/commit/59d5d1faf6fae1a3d3e6713d83303bfc60c55daf))
* **WebAPI:** Revert ([f38289b](https://github.com/Reaparr/Reaparr/commit/f38289bcba167b20170fa7fe3f853364a634d5d9))

# [0.38.0-dev.22](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.21...v0.38.0-dev.22) (2026-07-10)


### Bug Fixes

* **WebAPI:** Disable paging for zero page size ([691524e](https://github.com/Reaparr/Reaparr/commit/691524ef58ad92e0622f617f78e9fb05d14ef7ff))


### Features

* **Web-UI:** Add loading indicators for filter menu and alphabet navigation ([84fb489](https://github.com/Reaparr/Reaparr/commit/84fb489ea2217844d57235665329f3ed460b2973))


### Performance Improvements

* **WebAPI:** Added media overview cache for the home page to greatly improve loading times ([acfbbee](https://github.com/Reaparr/Reaparr/commit/acfbbee3a2078b5458e10a10071505da398ab821))
* **Web-UI:** Batch page fetches, reduce overscan, and prefetch before scroll ([c09faec](https://github.com/Reaparr/Reaparr/commit/c09faeca126234b3a8580e08de4417dc505e588f))
* **Web-UI:** Defer thumbnail loading to viewport-visible rows only ([c4d77b1](https://github.com/Reaparr/Reaparr/commit/c4d77b139ddcc941c05b75055543cf190ebc28b4))
* **Web-UI:** Fetch filter metadata via dedicated endpoint instead of page response ([e91086c](https://github.com/Reaparr/Reaparr/commit/e91086ca1baa926141da1d723c079c828d2c5417))
* **Web-UI:** Optimize poster table render and scroll performance ([e0ff569](https://github.com/Reaparr/Reaparr/commit/e0ff569d11e68f68c21fcd1fb79aeda346c27365))
* **Web-UI:** Preload thumbnails for overscan rows with wider buffer ([691eafb](https://github.com/Reaparr/Reaparr/commit/691eafbf2f9408c66dc5244a62a1370d6265e0ab))
* **WebAPI:** Strip filter metadata from media page responses and serve lazily ([9ff0807](https://github.com/Reaparr/Reaparr/commit/9ff0807d48352c822de26e579057e9a4e87b41f5))

# [0.38.0-dev.21](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.20...v0.38.0-dev.21) (2026-06-07)


### Bug Fixes

* **WebAPI:** Allow download folder media type none ([5bb25b9](https://github.com/Reaparr/Reaparr/commit/5bb25b9d1a8d993170804bb3847a158a64b092b6))
* **WebAPI:** Correct all-library alphabet navigation order ([396eea4](https://github.com/Reaparr/Reaparr/commit/396eea4d92627786f3be398ea4f38b79ab9ecf16))
* **Web-UI:** Distinguish between disabled and inaccessible servers ([2039230](https://github.com/Reaparr/Reaparr/commit/20392309764672dcf3f944384366f05979465967))
* **WebAPI:** Fix incorrect job status updates due to missing listeners ([344d642](https://github.com/Reaparr/Reaparr/commit/344d6425be7aee83baaf8c65ada6e28f18dda5e8))
* **WebAPI:** Fixed an issue where inaccessible servers would still show up on the home page, this media is now always filtered out ([b4d08a6](https://github.com/Reaparr/Reaparr/commit/b4d08a6f96ee747e269dd466a0ca3f2aac52e91b))
* **WebAPI:** Fixed PlexLibrary not being sent after a RefreshLibraryMedia ([cb323bc](https://github.com/Reaparr/Reaparr/commit/cb323bcea6091d1d76d3154d0397d4ee80176663))
* **Web-UI:** Fixed the alphabetical order of the Plex libraries in the server drawer, now its movies, then tv-shows then the rest ([e344b0e](https://github.com/Reaparr/Reaparr/commit/e344b0e2f1b95a756925b39351a3ffcd15c8ee36))
* **Web-UI:** Handle empty job update payloads ([5d89ba8](https://github.com/Reaparr/Reaparr/commit/5d89ba8201631dcbcb38b49a18a3b8bb5b896388))
* **frontend:** mark inaccessible media header names ([4feeb97](https://github.com/Reaparr/Reaparr/commit/4feeb97b06d84710e9817a05d4eb681955d69b33))
* **WebAPI:** Preserve Plex library metrics during refresh ([6771d1c](https://github.com/Reaparr/Reaparr/commit/6771d1c57e500b200958bab92f6254efc2385a5a))
* **WebAPI:** Remove Database.EnsureCreated() from DbContext constructor, this was causing errors during unit tests ([26e7d12](https://github.com/Reaparr/Reaparr/commit/26e7d12ae2cf0023e40fe375081ab394ec0597f7))
* **Web-UI:** Show server name on fallback posters ([5490890](https://github.com/Reaparr/Reaparr/commit/549089046ab94c08875a3b9c8d61e6a1dee0ed14))
* **WebAPI:** Validate setup folder paths by existence ([5314d95](https://github.com/Reaparr/Reaparr/commit/5314d952eced13b8f685ae49283fcaef283f7449)), closes [#599](https://github.com/Reaparr/Reaparr/issues/599)


### Features

* **Web-UI:** Add strikethrough through the Plex server name on the Downloads Table when there is no Plex account that has access ([570b861](https://github.com/Reaparr/Reaparr/commit/570b861881a9034b8decb3e4d6ac51fb11c9ae4e))
* **Web-UI:** Added Plex server command button in the Server settings to delete a Plex Server ([baf277c](https://github.com/Reaparr/Reaparr/commit/baf277c3a6f8a3f29fbb0e12ec63d2c252af09ed))

# [0.38.0-dev.20](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.19...v0.38.0-dev.20) (2026-05-24)


### Bug Fixes

* **WebAPI:** Disable downloader disk precheck ([d8b0290](https://github.com/Reaparr/Reaparr/commit/d8b0290e9c5b99e8f7cfce4e63138ab58df06164))
* **WebAPI:** Fix integration test download flow ([1994ace](https://github.com/Reaparr/Reaparr/commit/1994acef27940ee6eedfe22ef451e12818612163))
* **WebAPI:** Persist verified download size ([166125a](https://github.com/Reaparr/Reaparr/commit/166125a8b8165c0226ba65f75b45d0c7013e4ad7))
* **WebAPI:** Reconcile missing direct download completion callback ([e344560](https://github.com/Reaparr/Reaparr/commit/e344560e4df7e4408ac96f43d1dc4e708078daf1))
* **WebAPI:** Refresh desktop ready state per window ([f9d526c](https://github.com/Reaparr/Reaparr/commit/f9d526c8f419f763927464ae2b8023ac453bf13c))
* **WebAPI:** Verify completed downloads before finishing ([#596](https://github.com/Reaparr/Reaparr/issues/596)) ([be20b8f](https://github.com/Reaparr/Reaparr/commit/be20b8f899f52fb1a166ce768bb1e1fb5e767c01))
* **WebAPI:** Verify completed downloads before finishing ([b1a72e2](https://github.com/Reaparr/Reaparr/commit/b1a72e24ca058b02e40bca3ab4b19e7bfb52c372))


### Features

* **WebAPI:** Added fallback for Windows to lauch browser if the desktop window fails to render thanks to missing webview ([334a7d9](https://github.com/Reaparr/Reaparr/commit/334a7d9dfebaa556a7912177def691d1e01cff31))

# [0.38.0-dev.19](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.18...v0.38.0-dev.19) (2026-05-23)


### Bug Fixes

* **WebAPI:** Add DownloadQueue retry cooldown ([fae413d](https://github.com/Reaparr/Reaparr/commit/fae413d8e1e45f3c904c653bd5a1f0c0a3349d24))
* **WebAPI:** Add retry cooldown and boot-time queue kick to DownloadQueue ([#586](https://github.com/Reaparr/Reaparr/issues/586)) ([5c412ee](https://github.com/Reaparr/Reaparr/commit/5c412eed2088129201a9c5078dfdf44e1d1dca2f))
* **WebAPI:** Add retry cooldown and boot-time queue kick to DownloadQueue ([8fd13ac](https://github.com/Reaparr/Reaparr/commit/8fd13acb6974e11a12eccba910e3f88fafacde9a))
* **WebAPI:** Add retry cooldown and boot-time queue kick to DownloadQueue ([3050f52](https://github.com/Reaparr/Reaparr/commit/3050f52093d26c6b42b5b7d686c1fdeabbc23c57))
* **WebAPI:** Backfill granted history events ([e767540](https://github.com/Reaparr/Reaparr/commit/e76754098a5be52ae8adece1458ba3055b372314))
* **WebAPI:** Fixed the contentChangedAt being seconds since library createdAt and not a unix since epoch ([19f46c2](https://github.com/Reaparr/Reaparr/commit/19f46c24940aa9819a11f8183eb5f981b61921c5))
* **WebAPI:** Normalize SQLite DateTime values to UTC ([ac87642](https://github.com/Reaparr/Reaparr/commit/ac87642271170d22c18eff9fd51b0b37b4f7373e))
* **WebAPI:** Recover zombie Downloading tasks left over from a crash ([#585](https://github.com/Reaparr/Reaparr/issues/585)) ([650d2aa](https://github.com/Reaparr/Reaparr/commit/650d2aa40c4b0f4c07812fceccd41dc730cb6c62))
* **WebAPI:** Recover zombie Downloading tasks left over from a crash ([35187f1](https://github.com/Reaparr/Reaparr/commit/35187f125523845c850368c5a648cbc991623869))
* **WebAPI:** Skip boot queue kick in integration tests ([57ffea1](https://github.com/Reaparr/Reaparr/commit/57ffea16e8ed22335e18feb9715c593f23b31a3c))
* **WebAPI:** Stabilize DownloadQueue restart integration test ([9c0d78d](https://github.com/Reaparr/Reaparr/commit/9c0d78dfcd4eb1b3eab9ce75b88f6d931099f361))


### Features

* **Web-UI:** Added a spinning animation next to Plex Servers that are currently syncing their Plex libaries ([36b8b1f](https://github.com/Reaparr/Reaparr/commit/36b8b1f20dd30cbc57d223ad513a028537337121))
* **WebAPI:** Added auto-recovery of DownloadTasks when Reaparr crashes and auto pause when downloading and Reaparr shutsdown ([b755760](https://github.com/Reaparr/Reaparr/commit/b7557606822d8c1b0d5827a1570d3f632def4385))
* **WebAPI:** Added periodic auto PlexLibrary media sync, every hour Reaparr will check if there is an update to a PlexLibrary ans resync the media ([890ce26](https://github.com/Reaparr/Reaparr/commit/890ce26709fca806cae1c21643467ae0e2e14524))
* **WebAPI:** Added PlexLibrary access history tracking to determine and look back if access to a library has been lost or gained ([edfd374](https://github.com/Reaparr/Reaparr/commit/edfd374472449805e9e9ecb14e9da8fc58699380))
* **Web-UI:** Added translations for the new AutoPaused and AutoMovePaused ([25fc94c](https://github.com/Reaparr/Reaparr/commit/25fc94c7af54f11fcf3518c541454500af9d0cd8))
* **WebAPI:** Auto-recovery for Downloadtasks that are downloading or moving media files to destination when Reaparr shuts down or crashes ([f436d1c](https://github.com/Reaparr/Reaparr/commit/f436d1cec86b32859bc3382dcb3ae1ed59a61adb))
* **Web-UI:** First draft version of Plex library access history overview ([c7123ab](https://github.com/Reaparr/Reaparr/commit/c7123abed23253c6b95961ade7a1b654c8580a21))
* **Web-UI:** There is now a Library Access timeline visible under settings => Accounts => Scroll to bottom to view ([7728655](https://github.com/Reaparr/Reaparr/commit/7728655ba885ce6b82c2454dd9b077524df018f8))


### Reverts

* **WebAPI:** Revert migration ([b1cb378](https://github.com/Reaparr/Reaparr/commit/b1cb378cbe5d93c77be1a438878c4225ace7f1d2))
* **WebAPI:** Revert migraton ([59d5d1f](https://github.com/Reaparr/Reaparr/commit/59d5d1faf6fae1a3d3e6713d83303bfc60c55daf))

# [0.38.0-dev.18](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.17...v0.38.0-dev.18) (2026-05-17)


### Bug Fixes

* **WebAPI:** Auto-retry downloads after ServerUnreachable, Error, DownloadClientError ([#584](https://github.com/Reaparr/Reaparr/issues/584)) ([957ef85](https://github.com/Reaparr/Reaparr/commit/957ef8519fd1a26aebf5cfae3410017da63f9413))
* **WebAPI:** Auto-retry downloads after ServerUnreachable, Error, DownloadClientError ([de67420](https://github.com/Reaparr/Reaparr/commit/de67420dd22ae78d709903e37f5b0dfde8f86e06))
* **WebAPI:** Escape search query values that might mess up the search ([2757865](https://github.com/Reaparr/Reaparr/commit/27578653e9230aa1b6d3ea6ba0e0ed2a78bfbfbe))
* **WebAPI:** Fixed some sorting in the media overview to be doubly sorted leading to inconsisten sorting ([ce8475b](https://github.com/Reaparr/Reaparr/commit/ce8475bb6f330b459dbd40fed07dbbe5299285ee))
* **Web-UI:** Fixed the sorting on MediaTable view ([70fb472](https://github.com/Reaparr/Reaparr/commit/70fb472697d1e7bbfa5ba2b762992bf7bd6139fa))
* **WebAPI:** Handle HTTP retry exceptions ([e797641](https://github.com/Reaparr/Reaparr/commit/e797641b09aa7c4e9ff8c3d24807caa5da55da91))
* **WebAPI:** Handle zero media size fallback ([3f1dee5](https://github.com/Reaparr/Reaparr/commit/3f1dee59cc6b1a036626be5e004d44a3d94f4533))
* **data:** rehydrate persisted tv show IDs before season/episode inserts ([da4e1ac](https://github.com/Reaparr/Reaparr/commit/da4e1acae9190d73898ee2063dc2f910414415a1))
* **Web-UI:** Remove index column from mediaoverview table, its obsolete ([c2d0a52](https://github.com/Reaparr/Reaparr/commit/c2d0a523d6facec7bba9137240b06e5a1b88d4de))
* **Web-UI:** Shortend the debounce time from searching from 3 to 1 seconds to make it more reactive ([df3010e](https://github.com/Reaparr/Reaparr/commit/df3010e64b69cb38140bae9e71464964655c13e5))
* **WebAPI:** When restarting a DownloadTask, it will refetch the latest media source data ([2befea1](https://github.com/Reaparr/Reaparr/commit/2befea10b949cbeaddc25dc96248f1266e896612))


### Features

* **WebAPI:** Add retry mechanism to poster image loading ensuring far less image time-outs ([3a80841](https://github.com/Reaparr/Reaparr/commit/3a80841351d47b5c00d3862238a7093b39ee1b0f))
* **Web-UI:** Auto scroll to the last scroll position when navigating from and to the media overview ([45afb8f](https://github.com/Reaparr/Reaparr/commit/45afb8f9e96c0800f8cb34241f7524be5dabfc3d))
* **Web-UI:** Filtering media in the MediaOverview will now only display options that will give results ([1fd0165](https://github.com/Reaparr/Reaparr/commit/1fd01654c8e51eac92533aa4f9dbf122e25f335f))
* **WebAPI:** Implemented FlexQuery.NET as the new media filter and sorter ([e9c6da3](https://github.com/Reaparr/Reaparr/commit/e9c6da36a6601986c3aa71efe15cbed0bb128327))
* **Web-UI:** Reaparr now reloads a media overview with the last set filter and sort parameters ([ff55764](https://github.com/Reaparr/Reaparr/commit/ff55764bd5dda0dd86d37368c605f938e2afb64a))
* **Web-UI:** When clicking on a media flter menu item again, after it is checked, will now uncheck it ([c30b696](https://github.com/Reaparr/Reaparr/commit/c30b6969216272c2ca60f6d834fdd634c8a99380))


### Performance Improvements

* **Web-UI:** Implement paginated loading for media poster pages, this should make first-load much faster ([cacdfc5](https://github.com/Reaparr/Reaparr/commit/cacdfc53abc8e548ae79865bfda1c4010f83f8af))
* **Web-UI:** Made some medium performance improvement on the poster media table to speed up rendering ([abc9ecf](https://github.com/Reaparr/Reaparr/commit/abc9ecf74a7d584c277ece071057cc0fa63a5a19))
* **Web-UI:** Made some performance improvement Mediaposter to use less nested components to speed up rendering ([f6c3896](https://github.com/Reaparr/Reaparr/commit/f6c38969f65e71b2663730ad20deb0048f68e342))
* **WebAPI:** Move media sorting calculations to back-end to increase performance in the the front-end ([b1905f8](https://github.com/Reaparr/Reaparr/commit/b1905f835bb1de436da3cdcd54840c18df240ed0))


### Reverts

* **WebAPI:** Revert ([f38289b](https://github.com/Reaparr/Reaparr/commit/f38289bcba167b20170fa7fe3f853364a634d5d9))

# [0.38.0-dev.17](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.16...v0.38.0-dev.17) (2026-05-07)


### Bug Fixes

* **WebAPI:** Add donation link to the docker logging at the top ([b116b23](https://github.com/Reaparr/Reaparr/commit/b116b23f5c205445cba891443af6f67f8dbe98de))
* **Web-UI:** Correct docker runtime config platform and version baking ([360db11](https://github.com/Reaparr/Reaparr/commit/360db117fbd9a4210d30fb74cf4834735f7fa73e))
* **WebAPI:** enforce init-adduser before reaparr service startup ([7de81cb](https://github.com/Reaparr/Reaparr/commit/7de81cb392c14e2ef1a31f0ef7ed187eacced5b0))
* **WebAPI:** Finally fixed a several year old problem where Reaparr would crash the first time and then work afterwards! It now starts up much faster the first time ([f58e989](https://github.com/Reaparr/Reaparr/commit/f58e9891212337082a0669a504f693c0cf3e7e43))

# [0.38.0-dev.16](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.15...v0.38.0-dev.16) (2026-05-06)


### Bug Fixes

* **docker:** use PUID/PGID ownership for /Config ([#579](https://github.com/Reaparr/Reaparr/issues/579)) ([f5b80ac](https://github.com/Reaparr/Reaparr/commit/f5b80acc0e53337a84b7257a2d7e535660e3ebd4))
* **docker:** use PUID/PGID ownership for /Config ([87b5308](https://github.com/Reaparr/Reaparr/commit/87b53089afa224a6a2f48a630b08717dfa73912b))

# [0.38.0-dev.15](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.14...v0.38.0-dev.15) (2026-05-06)


### Bug Fixes

* **WebAPI:** Fix Docker /Config ownership bootstrap for [#578](https://github.com/Reaparr/Reaparr/issues/578) ([6e491a2](https://github.com/Reaparr/Reaparr/commit/6e491a227d283c92c697c1f4eae91b5600a67e3e))
* **WebAPI:** Fix missing log text in tests ([a5e18e1](https://github.com/Reaparr/Reaparr/commit/a5e18e174e69dc201d63a88bb37f15a7365887ef))
* **WebAPI:** Fixed the missing new line between logging ([1ffd5ef](https://github.com/Reaparr/Reaparr/commit/1ffd5efdee7eb885b68d35fa6f542d5cb1a194a1))
* **WebAPI:** Handle disabled server updates ([c91c8ee](https://github.com/Reaparr/Reaparr/commit/c91c8ee767b78df0957f4b187487774fdc81bfde))
* **WebAPI:** Queue inspect jobs with resilient disabled-server filtering ([79b769b](https://github.com/Reaparr/Reaparr/commit/79b769b68b85300f5343a5729af55478d80e7016))
* **Web-UI:** Redirect to login only on unauthorized ([2d2b52e](https://github.com/Reaparr/Reaparr/commit/2d2b52eccae7399a380a4a87572092d64f74721f))
* **WebAPI:** Return conflict for disabled server ([184e962](https://github.com/Reaparr/Reaparr/commit/184e96219d07185bb528ae3c5eb525b379f3c567))
* **WebAPI:** Stabilize PublicApi torrent tests and clean test logging ([95516fc](https://github.com/Reaparr/Reaparr/commit/95516fc2dac0df357cb54fb07e968d60c55d2194))
* **Web-UI:** Stop background render loop when animation is disabled ([b521a56](https://github.com/Reaparr/Reaparr/commit/b521a566e26b99f0429622b7bd933bcee98d68ff)), closes [#573](https://github.com/Reaparr/Reaparr/issues/573)


### Features

* **WebAPI:** Added an override to mark a server as owned such that it will not be used in Sonarr/Radarr results. This prevents downloading your own media already in your possession ([dcbb909](https://github.com/Reaparr/Reaparr/commit/dcbb90960b82f4b9cb3ae5362f2308ad3db114b9))
* **Web-UI:** Added IsOwned and IsEnabled toggles on the Server Settings dialog ([dc70c58](https://github.com/Reaparr/Reaparr/commit/dc70c586e6c28476a03ce10f296c5f411ffa869a))
* **Web-UI:** Removed the hide button in the Plex server settings, this is now meant to be hidden/disabled through the Server Configuration tab ([29166a8](https://github.com/Reaparr/Reaparr/commit/29166a8a07c745532465240ab52b2a6af625ed23))

# [0.38.0-dev.14](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.13...v0.38.0-dev.14) (2026-05-03)


### Bug Fixes

* **WebAPI:** Ensure Reaparr closes fully when applying the update ([2869197](https://github.com/Reaparr/Reaparr/commit/2869197a9de7f38c9b38df4941f079317990f1f2))
* **WebAPI:** Fix a startup issue on Windows desktop where background jobs were not found ([69ee826](https://github.com/Reaparr/Reaparr/commit/69ee8262b60c077633e40e4e90d8bc9e42da6ae7))
* **WebAPI:** Fix Build unit test dependency wiring ([f0a2da0](https://github.com/Reaparr/Reaparr/commit/f0a2da068bde95bceab87eff1ba5e0b46a28a7b1))
* **build:** harden desktop packaging filesystem paths ([46dc9bc](https://github.com/Reaparr/Reaparr/commit/46dc9bcda8d62ee3de4fadff07a478da0590990f))


### Features

* **WebAPI:** Add error dialog on startup if Reaparr in desktop mode fails ([9e82a78](https://github.com/Reaparr/Reaparr/commit/9e82a785ebb596f31339df794d324f1ecf1e1e95))
* **WebAPI:** Add error pop-up dialog when Reaparr fails to start in desktop mode ([d7baf59](https://github.com/Reaparr/Reaparr/commit/d7baf59b8f40ddab0e3da9603ebc21d3c0bd5121))

# [0.38.0-dev.13](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.12...v0.38.0-dev.13) (2026-05-01)


### Bug Fixes

* **WebAPI:** Fix the Reaparr app not exiting on applying update ([e90ba14](https://github.com/Reaparr/Reaparr/commit/e90ba1424071f3220e7b029caaa60160e9927a1c))

# [0.38.0-dev.12](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.11...v0.38.0-dev.12) (2026-04-27)

# [0.38.0-dev.11](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.10...v0.38.0-dev.11) (2026-04-27)


### Bug Fixes

* **WebAPI:** Ensure per-runtime release channels and frontend depende… ([#572](https://github.com/Reaparr/Reaparr/issues/572)) ([7b01438](https://github.com/Reaparr/Reaparr/commit/7b014386a6139166cbb473f768efe861cb59a4b0))
* **WebAPI:** Ensure per-runtime release channels and frontend dependency install ([7db5301](https://github.com/Reaparr/Reaparr/commit/7db5301553f6ee3c6b525436d53286a47360a23c))
* **WebAPI:** Fix Dockerfile stage parsing ([6ae517e](https://github.com/Reaparr/Reaparr/commit/6ae517ebbbd206b02f49d84c653fc44c0e0cd893))
* **WebAPI:** Handle malformed release tags and improve update check fallback ([1da7d05](https://github.com/Reaparr/Reaparr/commit/1da7d05a4e7de003987a4fba27d120ba78ef56dd))


### Features

* **Web-UI:** Added progress to the download update pop-up ([931e110](https://github.com/Reaparr/Reaparr/commit/931e110bbc0583f87c2dca297d9d4ab9e36336e6))

# [0.38.0-dev.10](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.9...v0.38.0-dev.10) (2026-04-24)


### Bug Fixes

* **WebAPI:** Use per-rid release channels for desktop uploads ([3b3122b](https://github.com/Reaparr/Reaparr/commit/3b3122b8386d9f7ef1693947e3ee3341858738ee))

# [0.38.0-dev.9](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.8...v0.38.0-dev.9) (2026-04-24)


### Bug Fixes

* **WebAPI:** Patch vulnerable ASP.NET Core packages ([f5aad38](https://github.com/Reaparr/Reaparr/commit/f5aad380886fa0d9808e1ba56d1ed5a20226bdf4))
* **WebAPI:** Serialize desktop release uploads ([197d51e](https://github.com/Reaparr/Reaparr/commit/197d51e3f335e2038a4936bbc3fe48d516964903))

# [0.38.0-dev.10](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.9...v0.38.0-dev.10) (2026-04-24)


### Bug Fixes

* **WebAPI:** Patch vulnerable ASP.NET Core packages ([f5aad38](https://github.com/Reaparr/Reaparr/commit/f5aad380886fa0d9808e1ba56d1ed5a20226bdf4))

# [0.38.0-dev.9](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.8...v0.38.0-dev.9) (2026-04-23)


### Bug Fixes

* **WebAPI:** Serialize desktop release uploads ([197d51e](https://github.com/Reaparr/Reaparr/commit/197d51e3f335e2038a4936bbc3fe48d516964903))

# [0.38.0-dev.8](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.7...v0.38.0-dev.8) (2026-04-23)


### Bug Fixes

* **WebAPI:** Correct desktop packaging executable names ([9816443](https://github.com/Reaparr/Reaparr/commit/981644382c269370e8bf5b13db28d1ce602bf340))
* **WebAPI:** Ensure desktop window is disposed and re-created after closing to background ([ed3e198](https://github.com/Reaparr/Reaparr/commit/ed3e198f085224d58513ff62c0492b2bc4cda684))
* **WebAPI:** Fixed an issue where the default FolderPaths could not be updated ([23030a9](https://github.com/Reaparr/Reaparr/commit/23030a9fe258c753560dacf8cd0911b776934f40))
* **WebAPI:** Fixed external links not opening the browser when running Reaparr in Desktop mode ([5fce646](https://github.com/Reaparr/Reaparr/commit/5fce646a1f7b961d2c94e37a3646bd63608f83f2))
* **Web-UI:** Fixed some setup page translations not updating when the language is changes ([a7a8b37](https://github.com/Reaparr/Reaparr/commit/a7a8b37a6872f7ce69a949826686d3719123b557))
* **WebAPI:** Log failures when notifying Radarr and Sonarr on startup ([5ad58e7](https://github.com/Reaparr/Reaparr/commit/5ad58e735558400526b283d1de446207de05723f))
* **Web-UI:** Set app version and platform when not logged in yet ([fb11391](https://github.com/Reaparr/Reaparr/commit/fb1139164029b7b710e193ccf62f447050de23ae))
* **WebAPI:** Skip update check if Velopack is not installed in desktop mode ([50cdca1](https://github.com/Reaparr/Reaparr/commit/50cdca11fc58189084d7f1f0bbb42d8979e51278))


### Features

* **WebAPI:** Added seperate build CLI project for the desktop build workflow ([2df35d6](https://github.com/Reaparr/Reaparr/commit/2df35d637a3d8c8465238e208a95de31f04e34dd))

# [0.38.0-dev.7](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.6...v0.38.0-dev.7) (2026-04-22)


### Bug Fixes

* **WebAPI:** Fixed an issue where the logging to log files contained a lot of noise making it unreadable ([8add639](https://github.com/Reaparr/Reaparr/commit/8add639722cdb087cdbebd431f0a82449b8baac1))
* **WebAPI:** Fixed new database creation failing due to incorrect seed data ([4365743](https://github.com/Reaparr/Reaparr/commit/43657439f3e2b6657caa83f882ee65b5dc30d7fe))


### Features

* **Web-UI:** Add app update download progress tracking in the update dialog ([c478334](https://github.com/Reaparr/Reaparr/commit/c478334d3c32c6e76abd10755c806b63abaf04ed))
* **WebAPI:** Add development middleware to disable authentication through environment variable I_AM_DUMB_SO_DISABLE_AUTHENTICATION ([990aa33](https://github.com/Reaparr/Reaparr/commit/990aa335565e65c3659ef553cc23aad631794348))
* **Web-UI:** Added a update dialog when there is a new Reaparr release ([58da7db](https://github.com/Reaparr/Reaparr/commit/58da7dbbe6a3b9bab3bcaf6c1379fac594805b96))
* **WebAPI:** Added option to add a GITHUB_TOKEN to Reaparr to circumvent the Github ratelimits ([15ca26a](https://github.com/Reaparr/Reaparr/commit/15ca26ae0f83b2106c61c10c87d4ac2e82e8430d))
* **Web-UI:** Enhance log viewer with selection and copy functionality ([ecd0c0d](https://github.com/Reaparr/Reaparr/commit/ecd0c0daddb19dfc3ecaabc771937f5e7640ad4e))
* **Web-UI:** Enhance log viewer with selection and copy functionality ([d26c577](https://github.com/Reaparr/Reaparr/commit/d26c577260ad5beea64415aa2c97b4f639c414d4))
* **WebAPI:** Implement app update download progress tracking ([aec0c6e](https://github.com/Reaparr/Reaparr/commit/aec0c6eb93b6dd3b5bca7c7eff7f2528cb30fae2))
* **WebAPI:** Implement desktop single-instance coordination and window lifecycle. This allows Reaparr to keep running in the background when the window closes on desktop ([fcbc34c](https://github.com/Reaparr/Reaparr/commit/fcbc34c1adccd969deff246356f1a082df6451b5))
* **WebAPI:** Implement GitHub releases fetching and mapping to showcase new updates ([a750151](https://github.com/Reaparr/Reaparr/commit/a750151a4923ef4a667304867de29b2090565839))
* **WebAPI:** Implement live log streaming via SignalR ([63a5492](https://github.com/Reaparr/Reaparr/commit/63a5492f9265208dccf0df4263d9de84e5fda11e))
* **Web-UI:** Implement live log viewer under Settings => Logs with realtime updates ([6bc494e](https://github.com/Reaparr/Reaparr/commit/6bc494e9e0b6f8f8dd13827ad7ff7d129b1bcef9))
* **WebAPI:** Implement update management endpoints for checking and applying updates ([0e32c45](https://github.com/Reaparr/Reaparr/commit/0e32c45673695c22162ecfe9d22adfb10e3c09fb))
* **WebAPI:** Implement update Reaparr check endpoint and related background job to keep checking every hour ([bb0ed40](https://github.com/Reaparr/Reaparr/commit/bb0ed409a3b286ecfcbb58b36ac94aa41c3cd76a))

# [0.38.0-dev.6](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.5...v0.38.0-dev.6) (2026-04-10)


### Bug Fixes

* **WebAPI:** Use per-architecture vpk channels to prevent release asset collisions ([a072f88](https://github.com/Reaparr/Reaparr/commit/a072f883d4b7f6d6ba703cf05b91cee6b4ad8ddb))

# [0.38.0-dev.5](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.4...v0.38.0-dev.5) (2026-04-10)


### Bug Fixes

* **WebAPI:** Add --channel flag to vpk upload steps to prevent OS auto-detection ([7278c40](https://github.com/Reaparr/Reaparr/commit/7278c40c0c8c369c619b0881afdfadf39fa44a24))

# [0.38.0-dev.4](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.3...v0.38.0-dev.4) (2026-04-10)


### Bug Fixes

* **WebAPI:** Serialize desktop uploads via dedicated job to prevent asset conflicts ([5d8493e](https://github.com/Reaparr/Reaparr/commit/5d8493e8b5b75db30b76c84ee3b8eb47db9ed264))

# [0.38.0-dev.3](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.2...v0.38.0-dev.3) (2026-04-10)


### Bug Fixes

* **WebAPI:** Add merge option to GitHub upload commands ([c17dbf0](https://github.com/Reaparr/Reaparr/commit/c17dbf02047d7773740e373abd6af864a03b5978))

# [0.38.0-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.38.0-dev.1...v0.38.0-dev.2) (2026-04-10)


### Bug Fixes

* **WebAPI:** Update Node.js version and improve CI workflow configurations ([285de8f](https://github.com/Reaparr/Reaparr/commit/285de8f5ff237884917a2eca9ea74b1b5211cf0c))
* **WebAPI:** Update notification messages and Node.js version in release pipeline ([9ff413d](https://github.com/Reaparr/Reaparr/commit/9ff413d2a3b224c13b53e463795a86e08fce191f))

# [0.38.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.37.0...v0.38.0-dev.1) (2026-04-09)


### Bug Fixes

* **Web-UI:** Enforce auth status and logout consistency ([8020869](https://github.com/Reaparr/Reaparr/commit/802086997e5fbea4029e50928c3f27d4fd6ea446))
* **Web-UI:** Guard failed Plex account mutations ([e3bb89a](https://github.com/Reaparr/Reaparr/commit/e3bb89a6aa47f16148b12cd7260e5ec902022bd0))
* **Web-UI:** Guard failed server connection refreshes ([326d56f](https://github.com/Reaparr/Reaparr/commit/326d56f43a145e8c91691a636c362137e2cc4187))
* **Web-UI:** Harden locale selection and i18n guards ([e29e72f](https://github.com/Reaparr/Reaparr/commit/e29e72fb5492b7b5b225f44dffa900cb8934be23))
* **WebAPI:** Harden move download cancellation and job queueing ([efd281f](https://github.com/Reaparr/Reaparr/commit/efd281fd07403fc1ea6254d7019e3d2fcaa7a04c))
* **Web-UI:** Preserve download command failure state ([b42d3cb](https://github.com/Reaparr/Reaparr/commit/b42d3cb112247a059d1ec854103ec3de7e0a7d22))
* **Web-UI:** Refresh servers only after successful updates ([faffdd6](https://github.com/Reaparr/Reaparr/commit/faffdd63a52d060f222c671e063db342c7e9d777))
* **Web-UI:** Reset integration loading state on errors ([d7f615c](https://github.com/Reaparr/Reaparr/commit/d7f615c2fc8c721c57f7adcbcca519a3c3f2a3ac))
* **WebAPI:** Respect client-owned download failure statuses ([ccf8c42](https://github.com/Reaparr/Reaparr/commit/ccf8c42a89a0e91aab97fd5ed00d9dcd6328fa86))
* **Web-UI:** Stabilize account dialog validation and save flows ([6d0d4a6](https://github.com/Reaparr/Reaparr/commit/6d0d4a6b35eb0fb9cdfd7c02a067686f3d02e910))
* **Web-UI:** Stabilize settings autosave subscriptions ([e7c4a5c](https://github.com/Reaparr/Reaparr/commit/e7c4a5c057f5620d5cfa7d7dd34a8e012a83cd44))
* **Web-UI:** Surface invalid Plex credentials in account dialog ([5067b2c](https://github.com/Reaparr/Reaparr/commit/5067b2cb77fd20f754e9d817bad14e085b4b0ed9))


### Features

* **WebAPI:** Add desktop build workflow and configuration for multiple platforms like Linux, MacOS and Windows ([e6a036d](https://github.com/Reaparr/Reaparr/commit/e6a036dbb0fa8d8205f0c6190547b6391853f7e7))
* **WebAPI:** Add scripts and configurations for desktop builds on Linux, MacOS, and Windows ([3169fff](https://github.com/Reaparr/Reaparr/commit/3169fff04257dd84c4ca44a2a04b36ce3666c424))


### Reverts

* **Web-UI:** Revert "improvements" ([cb481da](https://github.com/Reaparr/Reaparr/commit/cb481da31a34e222af0c69daf0fedb742b7b282f))
* **Web-UI:** Revert "improvements" ([359583a](https://github.com/Reaparr/Reaparr/commit/359583a165710fc0bfdfbe55729fac743e5202e5))

# [0.37.0](https://github.com/Reaparr/Reaparr/compare/v0.36.0...v0.37.0) (2026-03-31)


### Bug Fixes

* **WebAPI:** Accept GUID array payload for clear tasks endpoint ([04caa23](https://github.com/Reaparr/Reaparr/commit/04caa2394d5f2c5325eb0a20f4b178002356b8fd))
* **WebAPI:** Add Docker network hint to Sonarr/Radarr download client setup errors ([0ee8a69](https://github.com/Reaparr/Reaparr/commit/0ee8a697ec35759762ad2b153714f2ec7ff5936d))
* **WebAPI:** Add missing AllowStreamDownloader Bogus rule in FakeData ([5a97f72](https://github.com/Reaparr/Reaparr/commit/5a97f728e93eb5752b282a3aca8dfe1e3b4fcaaa))
* **Web-UI:** Add timeout to success notification after download task log deletion ([6b6bb7c](https://github.com/Reaparr/Reaparr/commit/6b6bb7c79ff6ef81276da08a4f47ba9960e6b410))
* **Web-UI:** Added timeout to misconfigured errors for sonarr/radarr integration ([1ef8be4](https://github.com/Reaparr/Reaparr/commit/1ef8be4f6c8ae93ab7b3924a24662b8b6e785250))
* **Web-UI:** Align actions column to start ([36eeba0](https://github.com/Reaparr/Reaparr/commit/36eeba0d922af34a5f3d8a3b1eb839f65739a932))
* **WebAPI:** Align downloader temp handling and move cleanup ([a6b524c](https://github.com/Reaparr/Reaparr/commit/a6b524c9d101742cf75d857932c48ca0b39aa251))
* **WebAPI:** Avoid destructuring unsafe API request payloads ([ffa36c0](https://github.com/Reaparr/Reaparr/commit/ffa36c089b192d9af7dd3a3d50ce4f857cda4d59))
* **WebAPI:** Avoid double disposal of dash wrapper ([a414b3c](https://github.com/Reaparr/Reaparr/commit/a414b3c4ef27d2c851565b835dafc610b63d1a90))
* **Web-UI:** Corrected the icon in the notifications ([590215d](https://github.com/Reaparr/Reaparr/commit/590215dffc7389bdab201d655addfce54e427e36))
* **WebAPI:** Deauthenticate Radarr/Sonarr on Reaparr restart to force them to reauthenticate and ensure no stale auth remains in Radarr/Sonarr ([af39934](https://github.com/Reaparr/Reaparr/commit/af39934cab64678f1b68661ab58cb0a43e73e76a))
* **Web-UI:** Don't show 0 byte sizes but instead show "-" as download speed in the downloads table ([f1da3c2](https://github.com/Reaparr/Reaparr/commit/f1da3c2d84fe0dac6bb53cacf62169b41992083b))
* **Web-UI:** Enforce strict DownloadPatch MessagePack tuple parsing ([a33b4d7](https://github.com/Reaparr/Reaparr/commit/a33b4d714566b713db55c6f85e8f379b4baa3c55))
* **WebAPI:** Ensure completed download patches report full progress ([a3d3f0a](https://github.com/Reaparr/Reaparr/commit/a3d3f0af09e30aa065e203bd3f7b82b8c0dda806))
* **WebAPI:** Fix double-delete, EF UNION translation, torrent delete regressions, and key-based delete refactor ([e7377a9](https://github.com/Reaparr/Reaparr/commit/e7377a9e9d946679b7455ebc3fc54fadf973f697))
* **WebAPI:** Fix the downloads getting stuck and not continuing ([de5671d](https://github.com/Reaparr/Reaparr/commit/de5671d1c0b56cdd5ecef219f5ac3d35777ef932))
* **WebAPI:** Fixed a bug that prevented completed downloads from moving to their destination if aborted ([12b56f4](https://github.com/Reaparr/Reaparr/commit/12b56f45ec74c72ff7d20f4f74391b16918a0470))
* **WebAPI:** Fixed a memory leak that happened due to incorrect disposal of downloading resources ([4d79097](https://github.com/Reaparr/Reaparr/commit/4d79097bde7a571df6ee04068edaec9e2e10fc4a))
* **WebAPI:** Fixed a minor count bug in generating tvshow fake data for testing ([da495e3](https://github.com/Reaparr/Reaparr/commit/da495e3e0cfc46e4e1d3343c1539ed4c8c8d68f3))
* **Web-UI:** Fixed all the broken documentation links due to the new Reaparr.rocks website ([9f35b77](https://github.com/Reaparr/Reaparr/commit/9f35b7704b825806a54c4edfc67ad3176395356d))
* **WebAPI:** Fixed an annoying bug where downloads through Radarr/Sonarr would leave the downloaded files behind in the download directory ([cecaf4a](https://github.com/Reaparr/Reaparr/commit/cecaf4a05a02c96c18bc692e55e259ae41dfeabe))
* **WebAPI:** Fixed an issue where if the stream downloader would detect a direct download plex decision, it would fail. Now it will correctly download it as direct instead of fail ([6474f8e](https://github.com/Reaparr/Reaparr/commit/6474f8e1383186d608b6122025c0432d77c38c8a))
* **WebAPI:** Fixed an issue where it would make a download as DownloadFinished prematurely ([e8f74c9](https://github.com/Reaparr/Reaparr/commit/e8f74c947429b8cb036369ac8646deeab6cbe65e))
* **WebAPI:** Fixed an issue where some Plex servers needed download=1 in the download url, and some dont, now Reaparr will try both options always ([cd9cc4b](https://github.com/Reaparr/Reaparr/commit/cd9cc4bde2e2b143140fa483167be30614d9cba1))
* **WebAPI:** Fixed an issue where the check all connections dialog would show Done at the start while still starting ([0de6aba](https://github.com/Reaparr/Reaparr/commit/0de6aba145de9d7134d062fbc872b3d253284a5c))
* **WebAPI:** Fixed several annoying DownloadTask bugs where pausing the tvShow would pause all children and breaking the download flow ([8b820d3](https://github.com/Reaparr/Reaparr/commit/8b820d34471fd06db63718f5da48fcf6251a435c))
* **WebAPI:** Fixed start/stop downloadtask commands on tvshow/season ([60f96f8](https://github.com/Reaparr/Reaparr/commit/60f96f8dd44469260ab92a46be6711f819d3c5cd))
* **Web-UI:** Fixed the annoying columns in the downloadstable changing width when updating its values ([eaba7c9](https://github.com/Reaparr/Reaparr/commit/eaba7c9473989a8c18f3d8f603b30126ff7dcd84))
* **WebAPI:** Fixed the download task not being marked as Server Unreachable when the download fails mid download due to server offline ([ce42c28](https://github.com/Reaparr/Reaparr/commit/ce42c284493864eae4d9a8b66f49796818728d17))
* **Web-UI:** Fixed the media quality icon to have a darker background to make the quality text better readable ([7ee1c75](https://github.com/Reaparr/Reaparr/commit/7ee1c7509c08dfcf20f2d53b18a11ab8cf407511))
* **WebAPI:** Fixed the over logging when using the stream downloader ([21f15a8](https://github.com/Reaparr/Reaparr/commit/21f15a822a5068cca6396e10004d8735c0ff785d))
* **Web-UI:** Fixed the progress not updating correctly in the front-end + added tests to avoid this ([446da78](https://github.com/Reaparr/Reaparr/commit/446da78d5456e95bbb5ea20397ceb182c344740c))
* **Web-UI:** Fixed the title of dialogs, when long, will overlap with the UI underneath ([361fdfa](https://github.com/Reaparr/Reaparr/commit/361fdfa8508f7e6a89739b688790a5a18f0f9118))
* **WebAPI:** Format download progress percentage to two decimal places ([3027924](https://github.com/Reaparr/Reaparr/commit/302792401124383c0412caaa8474949319426eb5))
* **WebAPI:** Handle download client errors and improve error logging ([a8b75cb](https://github.com/Reaparr/Reaparr/commit/a8b75cb5160863bd0c9a7c30b75910deafaa9db7))
* **WebAPI:** Handle download task deletion status and notify front-end ([540168c](https://github.com/Reaparr/Reaparr/commit/540168cd5bdfd27b077586e4903bca1dc7426e15))
* **WebAPI:** Handle glibc loader copy on arm64 ([2199edf](https://github.com/Reaparr/Reaparr/commit/2199edfc50b1691eed0a8d926fb11ea9703927de))
* **WebAPI:** Handle request timeout and bad gateway errors in HTTP client ([5b07ff0](https://github.com/Reaparr/Reaparr/commit/5b07ff032a3f41897203797c07fa239d565f3397))
* Improve Docker networking UX for Sonarr/Radarr integration ([#560](https://github.com/Reaparr/Reaparr/issues/560)) ([762184f](https://github.com/Reaparr/Reaparr/commit/762184fc000b1bc9d9b4cafd53d23356f944f331))
* **WebAPI:** Map DASH network timeouts to server unreachable ([6a015cc](https://github.com/Reaparr/Reaparr/commit/6a015cc461569339a08b15b6c5525568fe0ce221))
* **WebAPI:** Mark DASH downloads complete only after process exit ([2c09e8d](https://github.com/Reaparr/Reaparr/commit/2c09e8dbea159e4bbfe8c63ed325b78c2a39837f))
* **WebAPI:** Mask sensitive request fields in logs ([5bceb4d](https://github.com/Reaparr/Reaparr/commit/5bceb4dd5d996c924abca768f0687e12bc999375))
* **WebAPI:** Pausing a download task will now set the downloadspeed to zero ([4f44136](https://github.com/Reaparr/Reaparr/commit/4f44136e6c0fa72886b8ceac490e437812d88de7))
* **WebAPI:** Persist final progress before marking downloads paused ([8b03ee2](https://github.com/Reaparr/Reaparr/commit/8b03ee2765ba8e67260201e9cfc0717ae00900a7))
* **WebAPI:** Preserve DataReceived on completed downloads ([225ce1f](https://github.com/Reaparr/Reaparr/commit/225ce1f3641aef5e3d18487df07834e04f410a81))
* **WebAPI:** Preserve direct download completion progress ([24983fa](https://github.com/Reaparr/Reaparr/commit/24983fa32dd758ffe489fb7bce8a45de7bb42429))
* **WebAPI:** Prevent cleanup from deleting shared download folders ([105f334](https://github.com/Reaparr/Reaparr/commit/105f334eab15c1a58d9b1590e3bab36c397d6a10))
* **WebAPI:** Prevent double disposal of PlexDownloadClient during shutdown ([383cf27](https://github.com/Reaparr/Reaparr/commit/383cf27b0770a47487cc1fc19fce37370117ec33))
* **WebAPI:** Prevent the DownloadQueue to continue downloading when the server is offline ([8e1525f](https://github.com/Reaparr/Reaparr/commit/8e1525f89612e338b49d8d82db02c8fc210f7187))
* **Web-UI:** Remove any casts from SignalR patch parsing ([673b474](https://github.com/Reaparr/Reaparr/commit/673b474272050f89201c5573947d1572d049832b))
* **WebAPI:** Remove invalid DirectoryMeta includes from torrent queries ([d77ebe9](https://github.com/Reaparr/Reaparr/commit/d77ebe97691814d7e1d604cd9cca07f06c653221))
* **WebAPI:** Remove orphaned download task parents after child cleanup ([029fafd](https://github.com/Reaparr/Reaparr/commit/029fafd4ec4ef43dbe7b2f25e7971ecc4f8d4007))
* **WebAPI:** Replace HttpClientHandler with SocketsHttpHandler for proper TLS negotiation ([b584aa3](https://github.com/Reaparr/Reaparr/commit/b584aa343e3561f9e03fcfcb8c58a65a1abece67))
* **WebAPI:** Replace pre-allocated stream with directory creation and disk space check before downloads ([77d1f6e](https://github.com/Reaparr/Reaparr/commit/77d1f6e9ef9e14d7c4d25df7f0d51e4182979ee8))
* **WebAPI:** Reset paused task progress and prioritize unreachable tasks ([369db11](https://github.com/Reaparr/Reaparr/commit/369db11599037af1dc6c45ded609c64731299e27))
* **WebAPI:** Resolve ambiguous Result.Try overload in DashMpdCliWrapper event loop ([a820eb8](https://github.com/Reaparr/Reaparr/commit/a820eb8a7ae38535c9bb6be9aac5949ba8d46405))
* **WebAPI:** Resolved issue where Sonarr/Radarr would send delete all and nothing would happen ([94de81b](https://github.com/Reaparr/Reaparr/commit/94de81bfcb02c44220c8aacdafddf40ac8e19387))
* **WebAPI:** Set FullTitle for PlexMovie in mapper ([801396f](https://github.com/Reaparr/Reaparr/commit/801396f43b9022bda20e37217c7219878085777a))
* **Web-UI:** Show dynamic callback URL alert in Sonarr/Radarr integration settings ([8ec2b5e](https://github.com/Reaparr/Reaparr/commit/8ec2b5ed73a71b84a5c7737aed02ca3331f98ecb))
* **Web-UI:** Show server success when any connection ok ([58a54cf](https://github.com/Reaparr/Reaparr/commit/58a54cf6de61e5ae4ea7d24614a1ccae34cce297))
* **WebAPI:** Skip pausing download tasks that are not actively downloading ([45fc87b](https://github.com/Reaparr/Reaparr/commit/45fc87be0feb95647d3774e701daa4072d32880c))
* **WebAPI:** Some episodes of tv show would crash the download process when downloading. This is now fixed ([2bdbfd1](https://github.com/Reaparr/Reaparr/commit/2bdbfd15fe554b86f1f3ec64cf8ca3ca532e0fa4))
* **Web-UI:** Update Discord invite link ([8ba4f73](https://github.com/Reaparr/Reaparr/commit/8ba4f739f45853cf14e74488cda4b6ce0a7aa6c1))
* **WebAPI:** Update file transfer percentage during download move ([ea08362](https://github.com/Reaparr/Reaparr/commit/ea083626022dcc3d8f41e93dc4aaf4e2584aff27))


### Features

* **Web-UI:** A PlexServer can now be paused in its entirety by pressing the pause button in the DownloadTable ([8479ee2](https://github.com/Reaparr/Reaparr/commit/8479ee27fc85213357a7fae3894ca906a3383a2e))
* **Web-UI:** Add glowing tooltip styling for log levels ([2824e95](https://github.com/Reaparr/Reaparr/commit/2824e955d49be6acc918bfca5c215a19a9170eee))
* **Web-UI:** Added a timeline with filter options to the logging visible in the Download details dialog ([c36c9cb](https://github.com/Reaparr/Reaparr/commit/c36c9cb33505f3ae649d44b0f02506b0c7b9b513))
* **Web-UI:** Added a toggle to enable the stream downloader ([7930cad](https://github.com/Reaparr/Reaparr/commit/7930cad12d7360ca04eb192dfbee201d1c4bb116))
* **Web-UI:** Added an notification to the Sonarr/Radarr integration screen about the reverse proxy setup to do that first ([62e8593](https://github.com/Reaparr/Reaparr/commit/62e8593c90d253647541952f47e5b7a969395b6a))
* **Web-UI:** Change the default sorting for the Download task logs from new to old, from top to bottom ([b8e0acb](https://github.com/Reaparr/Reaparr/commit/b8e0acb17ad0bf61d8bd820e924a6f59227fbd79))
* **WebAPI:** Downloading with the stream downloader will now name it correctly based on the transcoded quality ([9b4805f](https://github.com/Reaparr/Reaparr/commit/9b4805f98b2925a5c58e2fc56a2ad1f31e18b06f))
* **Web-UI:** Improved the notifications to be compatible with the house style ([b7add9c](https://github.com/Reaparr/Reaparr/commit/b7add9c512548511d719165545485ebd17317c19))
* **Web-UI:** Moved the check all connections button to the Server Connections tab in the Plex server settings ([e2d4028](https://github.com/Reaparr/Reaparr/commit/e2d4028c9c34aadb15f38f32e3d09c48b2135ebf))
* **Web-UI:** Registering a Plex account with an authentication token, will now use the e-mail of that token as the display name, if the display name is empty ([f6acb75](https://github.com/Reaparr/Reaparr/commit/f6acb75907b030317e392d3f94c0be386c0800f3))
* **Web-UI:** Removed the DownloadBar and moved the clear completed button to each individual server downloads table ([4bbf531](https://github.com/Reaparr/Reaparr/commit/4bbf5316a1c01c44e3863ac2ceccbdd2d6fd8f3e))
* **Web-UI:** Warn users to read docs before enabling stream downloader ([af8c971](https://github.com/Reaparr/Reaparr/commit/af8c97167fa013f051056b4f833808dde8908d19))
* **WebAPI:** Wire up stream downloader selection and fallback in DownloadJob ([102d755](https://github.com/Reaparr/Reaparr/commit/102d75570c32e69465eec4008bea7750cb0b2c78))


### Performance Improvements

* **WebAPI:** Immediately broadcast first download progress to front-end ([4929d85](https://github.com/Reaparr/Reaparr/commit/4929d8572179ca0241b2f33ecd1dba7d9fd78c99))
* **Web-UI:** Improved download task progress update performance massively, by only sending the delta instead of the entire download task tree on every progress update ([f17bba8](https://github.com/Reaparr/Reaparr/commit/f17bba8c2e57746f7314f2a6f0a88e904be3dd3b))
* **WebAPI:** Incredible memory optimization when downloading media, instead of Reaparr using 1gb of memory it now uses around a 100mb! ([4c0bf42](https://github.com/Reaparr/Reaparr/commit/4c0bf420a3931ca84ad3478a57ca7cef0a279247))
* **WebAPI:** Split out the log tables based on the mediafile type, this is to increase performance of retrieiving DownloadTask logs ([48a6dd9](https://github.com/Reaparr/Reaparr/commit/48a6dd90e20e8323e18a0fd78c581b5c1f258809))
* **WebAPI:** Split out the log tables based on the mediafile type, this is to increase performance of retrieiving DownloadTask logs ([61635c4](https://github.com/Reaparr/Reaparr/commit/61635c49e3ea9451d842811a54d5d6528fbdd7e2))
* **WebAPI:** The starting of a download now happens much faster, from 11 secondes in some cases to 2 seconds ([0ed8171](https://github.com/Reaparr/Reaparr/commit/0ed81713dcb221498d7d8d50e9359e05fd39ac95))


### Reverts

* Revert "build(WebAPI): Added IsDownloadsPausedByUser to PlexServer table" ([571db66](https://github.com/Reaparr/Reaparr/commit/571db6610a5c4efd22ec1710b894dd9fad146a3c))

# [0.37.0-dev.5](https://github.com/Reaparr/Reaparr/compare/v0.37.0-dev.4...v0.37.0-dev.5) (2026-03-31)


### Bug Fixes

* **WebAPI:** Add Docker network hint to Sonarr/Radarr download client setup errors ([0ee8a69](https://github.com/Reaparr/Reaparr/commit/0ee8a697ec35759762ad2b153714f2ec7ff5936d))
* **WebAPI:** Deauthenticate Radarr/Sonarr on Reaparr restart to force them to reauthenticate and ensure no stale auth remains in Radarr/Sonarr ([af39934](https://github.com/Reaparr/Reaparr/commit/af39934cab64678f1b68661ab58cb0a43e73e76a))
* **WebAPI:** Fixed an annoying bug where downloads through Radarr/Sonarr would leave the downloaded files behind in the download directory ([cecaf4a](https://github.com/Reaparr/Reaparr/commit/cecaf4a05a02c96c18bc692e55e259ae41dfeabe))
* **WebAPI:** Fixed an issue where if the stream downloader would detect a direct download plex decision, it would fail. Now it will correctly download it as direct instead of fail ([6474f8e](https://github.com/Reaparr/Reaparr/commit/6474f8e1383186d608b6122025c0432d77c38c8a))
* **WebAPI:** Fixed the download task not being marked as Server Unreachable when the download fails mid download due to server offline ([ce42c28](https://github.com/Reaparr/Reaparr/commit/ce42c284493864eae4d9a8b66f49796818728d17))
* **WebAPI:** Handle download client errors and improve error logging ([a8b75cb](https://github.com/Reaparr/Reaparr/commit/a8b75cb5160863bd0c9a7c30b75910deafaa9db7))
* **WebAPI:** Handle download task deletion status and notify front-end ([540168c](https://github.com/Reaparr/Reaparr/commit/540168cd5bdfd27b077586e4903bca1dc7426e15))
* Improve Docker networking UX for Sonarr/Radarr integration ([#560](https://github.com/Reaparr/Reaparr/issues/560)) ([762184f](https://github.com/Reaparr/Reaparr/commit/762184fc000b1bc9d9b4cafd53d23356f944f331))
* **WebAPI:** Prevent the DownloadQueue to continue downloading when the server is offline ([8e1525f](https://github.com/Reaparr/Reaparr/commit/8e1525f89612e338b49d8d82db02c8fc210f7187))
* **WebAPI:** Remove invalid DirectoryMeta includes from torrent queries ([d77ebe9](https://github.com/Reaparr/Reaparr/commit/d77ebe97691814d7e1d604cd9cca07f06c653221))
* **Web-UI:** Show dynamic callback URL alert in Sonarr/Radarr integration settings ([8ec2b5e](https://github.com/Reaparr/Reaparr/commit/8ec2b5ed73a71b84a5c7737aed02ca3331f98ecb))
* **WebAPI:** Some episodes of tv show would crash the download process when downloading. This is now fixed ([2bdbfd1](https://github.com/Reaparr/Reaparr/commit/2bdbfd15fe554b86f1f3ec64cf8ca3ca532e0fa4))
* **Web-UI:** Update Discord invite link ([8ba4f73](https://github.com/Reaparr/Reaparr/commit/8ba4f739f45853cf14e74488cda4b6ce0a7aa6c1))

# [0.37.0-dev.4](https://github.com/Reaparr/Reaparr/compare/v0.37.0-dev.3...v0.37.0-dev.4) (2026-03-17)


### Bug Fixes

* **WebAPI:** Ensure completed download patches report full progress ([a3d3f0a](https://github.com/Reaparr/Reaparr/commit/a3d3f0af09e30aa065e203bd3f7b82b8c0dda806))
* **WebAPI:** Fix double-delete, EF UNION translation, torrent delete regressions, and key-based delete refactor ([e7377a9](https://github.com/Reaparr/Reaparr/commit/e7377a9e9d946679b7455ebc3fc54fadf973f697))
* **WebAPI:** Fix the downloads getting stuck and not continuing ([de5671d](https://github.com/Reaparr/Reaparr/commit/de5671d1c0b56cdd5ecef219f5ac3d35777ef932))
* **WebAPI:** Format download progress percentage to two decimal places ([3027924](https://github.com/Reaparr/Reaparr/commit/302792401124383c0412caaa8474949319426eb5))
* **WebAPI:** Handle request timeout and bad gateway errors in HTTP client ([5b07ff0](https://github.com/Reaparr/Reaparr/commit/5b07ff032a3f41897203797c07fa239d565f3397))
* **WebAPI:** Preserve direct download completion progress ([24983fa](https://github.com/Reaparr/Reaparr/commit/24983fa32dd758ffe489fb7bce8a45de7bb42429))
* **WebAPI:** Replace pre-allocated stream with directory creation and disk space check before downloads ([77d1f6e](https://github.com/Reaparr/Reaparr/commit/77d1f6e9ef9e14d7c4d25df7f0d51e4182979ee8))
* **WebAPI:** Update file transfer percentage during download move ([ea08362](https://github.com/Reaparr/Reaparr/commit/ea083626022dcc3d8f41e93dc4aaf4e2584aff27))


### Performance Improvements

* **WebAPI:** The starting of a download now happens much faster, from 11 secondes in some cases to 2 seconds ([0ed8171](https://github.com/Reaparr/Reaparr/commit/0ed81713dcb221498d7d8d50e9359e05fd39ac95))

# [0.37.0-dev.3](https://github.com/Reaparr/Reaparr/compare/v0.37.0-dev.2...v0.37.0-dev.3) (2026-03-12)


### Bug Fixes

* **Web-UI:** Add timeout to success notification after download task log deletion ([6b6bb7c](https://github.com/Reaparr/Reaparr/commit/6b6bb7c79ff6ef81276da08a4f47ba9960e6b410))
* **Web-UI:** Added timeout to misconfigured errors for sonarr/radarr integration ([1ef8be4](https://github.com/Reaparr/Reaparr/commit/1ef8be4f6c8ae93ab7b3924a24662b8b6e785250))
* **WebAPI:** Avoid double disposal of dash wrapper ([a414b3c](https://github.com/Reaparr/Reaparr/commit/a414b3c4ef27d2c851565b835dafc610b63d1a90))
* **Web-UI:** Corrected the icon in the notifications ([590215d](https://github.com/Reaparr/Reaparr/commit/590215dffc7389bdab201d655addfce54e427e36))
* **Web-UI:** Enforce strict DownloadPatch MessagePack tuple parsing ([a33b4d7](https://github.com/Reaparr/Reaparr/commit/a33b4d714566b713db55c6f85e8f379b4baa3c55))
* **WebAPI:** Fixed an issue where some Plex servers needed download=1 in the download url, and some dont, now Reaparr will try both options always ([cd9cc4b](https://github.com/Reaparr/Reaparr/commit/cd9cc4bde2e2b143140fa483167be30614d9cba1))
* **Web-UI:** Fixed the media quality icon to have a darker background to make the quality text better readable ([7ee1c75](https://github.com/Reaparr/Reaparr/commit/7ee1c7509c08dfcf20f2d53b18a11ab8cf407511))
* **WebAPI:** Fixed the over logging when using the stream downloader ([21f15a8](https://github.com/Reaparr/Reaparr/commit/21f15a822a5068cca6396e10004d8735c0ff785d))
* **Web-UI:** Fixed the progress not updating correctly in the front-end + added tests to avoid this ([446da78](https://github.com/Reaparr/Reaparr/commit/446da78d5456e95bbb5ea20397ceb182c344740c))
* **Web-UI:** Fixed the title of dialogs, when long, will overlap with the UI underneath ([361fdfa](https://github.com/Reaparr/Reaparr/commit/361fdfa8508f7e6a89739b688790a5a18f0f9118))
* **WebAPI:** Map DASH network timeouts to server unreachable ([6a015cc](https://github.com/Reaparr/Reaparr/commit/6a015cc461569339a08b15b6c5525568fe0ce221))
* **WebAPI:** Mark DASH downloads complete only after process exit ([2c09e8d](https://github.com/Reaparr/Reaparr/commit/2c09e8dbea159e4bbfe8c63ed325b78c2a39837f))
* **WebAPI:** Persist final progress before marking downloads paused ([8b03ee2](https://github.com/Reaparr/Reaparr/commit/8b03ee2765ba8e67260201e9cfc0717ae00900a7))


### Features

* **WebAPI:** Downloading with the stream downloader will now name it correctly based on the transcoded quality ([9b4805f](https://github.com/Reaparr/Reaparr/commit/9b4805f98b2925a5c58e2fc56a2ad1f31e18b06f))
* **Web-UI:** Improved the notifications to be compatible with the house style ([b7add9c](https://github.com/Reaparr/Reaparr/commit/b7add9c512548511d719165545485ebd17317c19))
* **Web-UI:** Warn users to read docs before enabling stream downloader ([af8c971](https://github.com/Reaparr/Reaparr/commit/af8c97167fa013f051056b4f833808dde8908d19))

# [0.37.0-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.37.0-dev.1...v0.37.0-dev.2) (2026-03-09)


### Bug Fixes

* **WebAPI:** Accept GUID array payload for clear tasks endpoint ([04caa23](https://github.com/Reaparr/Reaparr/commit/04caa2394d5f2c5325eb0a20f4b178002356b8fd))
* **Web-UI:** Align actions column to start ([36eeba0](https://github.com/Reaparr/Reaparr/commit/36eeba0d922af34a5f3d8a3b1eb839f65739a932))
* **WebAPI:** Align downloader temp handling and move cleanup ([a6b524c](https://github.com/Reaparr/Reaparr/commit/a6b524c9d101742cf75d857932c48ca0b39aa251))
* **WebAPI:** Avoid destructuring unsafe API request payloads ([ffa36c0](https://github.com/Reaparr/Reaparr/commit/ffa36c089b192d9af7dd3a3d50ce4f857cda4d59))
* **Web-UI:** Don't show 0 byte sizes but instead show "-" as download speed in the downloads table ([f1da3c2](https://github.com/Reaparr/Reaparr/commit/f1da3c2d84fe0dac6bb53cacf62169b41992083b))
* **WebAPI:** Fixed a memory leak that happened due to incorrect disposal of downloading resources ([4d79097](https://github.com/Reaparr/Reaparr/commit/4d79097bde7a571df6ee04068edaec9e2e10fc4a))
* **WebAPI:** Fixed a minor count bug in generating tvshow fake data for testing ([da495e3](https://github.com/Reaparr/Reaparr/commit/da495e3e0cfc46e4e1d3343c1539ed4c8c8d68f3))
* **Web-UI:** Fixed all the broken documentation links due to the new Reaparr.rocks website ([9f35b77](https://github.com/Reaparr/Reaparr/commit/9f35b7704b825806a54c4edfc67ad3176395356d))
* **WebAPI:** Fixed an issue where it would make a download as DownloadFinished prematurely ([e8f74c9](https://github.com/Reaparr/Reaparr/commit/e8f74c947429b8cb036369ac8646deeab6cbe65e))
* **WebAPI:** Fixed an issue where the check all connections dialog would show Done at the start while still starting ([0de6aba](https://github.com/Reaparr/Reaparr/commit/0de6aba145de9d7134d062fbc872b3d253284a5c))
* **WebAPI:** Fixed several annoying DownloadTask bugs where pausing the tvShow would pause all children and breaking the download flow ([8b820d3](https://github.com/Reaparr/Reaparr/commit/8b820d34471fd06db63718f5da48fcf6251a435c))
* **WebAPI:** Fixed start/stop downloadtask commands on tvshow/season ([60f96f8](https://github.com/Reaparr/Reaparr/commit/60f96f8dd44469260ab92a46be6711f819d3c5cd))
* **Web-UI:** Fixed the annoying columns in the downloadstable changing width when updating its values ([eaba7c9](https://github.com/Reaparr/Reaparr/commit/eaba7c9473989a8c18f3d8f603b30126ff7dcd84))
* **WebAPI:** Handle glibc loader copy on arm64 ([2199edf](https://github.com/Reaparr/Reaparr/commit/2199edfc50b1691eed0a8d926fb11ea9703927de))
* **WebAPI:** Mask sensitive request fields in logs ([5bceb4d](https://github.com/Reaparr/Reaparr/commit/5bceb4dd5d996c924abca768f0687e12bc999375))
* **WebAPI:** Pausing a download task will now set the downloadspeed to zero ([4f44136](https://github.com/Reaparr/Reaparr/commit/4f44136e6c0fa72886b8ceac490e437812d88de7))
* **WebAPI:** Preserve DataReceived on completed downloads ([225ce1f](https://github.com/Reaparr/Reaparr/commit/225ce1f3641aef5e3d18487df07834e04f410a81))
* **WebAPI:** Prevent cleanup from deleting shared download folders ([105f334](https://github.com/Reaparr/Reaparr/commit/105f334eab15c1a58d9b1590e3bab36c397d6a10))
* **WebAPI:** Prevent double disposal of PlexDownloadClient during shutdown ([383cf27](https://github.com/Reaparr/Reaparr/commit/383cf27b0770a47487cc1fc19fce37370117ec33))
* **Web-UI:** Remove any casts from SignalR patch parsing ([673b474](https://github.com/Reaparr/Reaparr/commit/673b474272050f89201c5573947d1572d049832b))
* **WebAPI:** Remove orphaned download task parents after child cleanup ([029fafd](https://github.com/Reaparr/Reaparr/commit/029fafd4ec4ef43dbe7b2f25e7971ecc4f8d4007))
* **WebAPI:** Reset paused task progress and prioritize unreachable tasks ([369db11](https://github.com/Reaparr/Reaparr/commit/369db11599037af1dc6c45ded609c64731299e27))
* **WebAPI:** Resolve ambiguous Result.Try overload in DashMpdCliWrapper event loop ([a820eb8](https://github.com/Reaparr/Reaparr/commit/a820eb8a7ae38535c9bb6be9aac5949ba8d46405))
* **WebAPI:** Resolved issue where Sonarr/Radarr would send delete all and nothing would happen ([94de81b](https://github.com/Reaparr/Reaparr/commit/94de81bfcb02c44220c8aacdafddf40ac8e19387))
* **WebAPI:** Set FullTitle for PlexMovie in mapper ([801396f](https://github.com/Reaparr/Reaparr/commit/801396f43b9022bda20e37217c7219878085777a))
* **Web-UI:** Show server success when any connection ok ([58a54cf](https://github.com/Reaparr/Reaparr/commit/58a54cf6de61e5ae4ea7d24614a1ccae34cce297))
* **WebAPI:** Skip pausing download tasks that are not actively downloading ([45fc87b](https://github.com/Reaparr/Reaparr/commit/45fc87be0feb95647d3774e701daa4072d32880c))


### Features

* **Web-UI:** A PlexServer can now be paused in its entirety by pressing the pause button in the DownloadTable ([8479ee2](https://github.com/Reaparr/Reaparr/commit/8479ee27fc85213357a7fae3894ca906a3383a2e))
* **Web-UI:** Add glowing tooltip styling for log levels ([2824e95](https://github.com/Reaparr/Reaparr/commit/2824e955d49be6acc918bfca5c215a19a9170eee))
* **Web-UI:** Added a timeline with filter options to the logging visible in the Download details dialog ([c36c9cb](https://github.com/Reaparr/Reaparr/commit/c36c9cb33505f3ae649d44b0f02506b0c7b9b513))
* **Web-UI:** Added an notification to the Sonarr/Radarr integration screen about the reverse proxy setup to do that first ([62e8593](https://github.com/Reaparr/Reaparr/commit/62e8593c90d253647541952f47e5b7a969395b6a))
* **Web-UI:** Change the default sorting for the Download task logs from new to old, from top to bottom ([b8e0acb](https://github.com/Reaparr/Reaparr/commit/b8e0acb17ad0bf61d8bd820e924a6f59227fbd79))
* **Web-UI:** Moved the check all connections button to the Server Connections tab in the Plex server settings ([e2d4028](https://github.com/Reaparr/Reaparr/commit/e2d4028c9c34aadb15f38f32e3d09c48b2135ebf))
* **Web-UI:** Registering a Plex account with an authentication token, will now use the e-mail of that token as the display name, if the display name is empty ([f6acb75](https://github.com/Reaparr/Reaparr/commit/f6acb75907b030317e392d3f94c0be386c0800f3))
* **Web-UI:** Removed the DownloadBar and moved the clear completed button to each individual server downloads table ([4bbf531](https://github.com/Reaparr/Reaparr/commit/4bbf5316a1c01c44e3863ac2ceccbdd2d6fd8f3e))


### Performance Improvements

* **WebAPI:** Immediately broadcast first download progress to front-end ([4929d85](https://github.com/Reaparr/Reaparr/commit/4929d8572179ca0241b2f33ecd1dba7d9fd78c99))
* **Web-UI:** Improved download task progress update performance massively, by only sending the delta instead of the entire download task tree on every progress update ([f17bba8](https://github.com/Reaparr/Reaparr/commit/f17bba8c2e57746f7314f2a6f0a88e904be3dd3b))
* **WebAPI:** Incredible memory optimization when downloading media, instead of Reaparr using 1gb of memory it now uses around a 100mb! ([4c0bf42](https://github.com/Reaparr/Reaparr/commit/4c0bf420a3931ca84ad3478a57ca7cef0a279247))
* **WebAPI:** Split out the log tables based on the mediafile type, this is to increase performance of retrieiving DownloadTask logs ([48a6dd9](https://github.com/Reaparr/Reaparr/commit/48a6dd90e20e8323e18a0fd78c581b5c1f258809))
* **WebAPI:** Split out the log tables based on the mediafile type, this is to increase performance of retrieiving DownloadTask logs ([61635c4](https://github.com/Reaparr/Reaparr/commit/61635c49e3ea9451d842811a54d5d6528fbdd7e2))


### Reverts

* Revert "build(WebAPI): Added IsDownloadsPausedByUser to PlexServer table" ([571db66](https://github.com/Reaparr/Reaparr/commit/571db6610a5c4efd22ec1710b894dd9fad146a3c))

# [0.37.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.36.0...v0.37.0-dev.1) (2026-02-26)


### Bug Fixes

* **WebAPI:** Add missing AllowStreamDownloader Bogus rule in FakeData ([5a97f72](https://github.com/Reaparr/Reaparr/commit/5a97f728e93eb5752b282a3aca8dfe1e3b4fcaaa))
* **WebAPI:** Fixed a bug that prevented completed downloads from moving to their destination if aborted ([12b56f4](https://github.com/Reaparr/Reaparr/commit/12b56f45ec74c72ff7d20f4f74391b16918a0470))
* **WebAPI:** Replace HttpClientHandler with SocketsHttpHandler for proper TLS negotiation ([b584aa3](https://github.com/Reaparr/Reaparr/commit/b584aa343e3561f9e03fcfcb8c58a65a1abece67))


### Features

* **Web-UI:** Added a toggle to enable the stream downloader ([7930cad](https://github.com/Reaparr/Reaparr/commit/7930cad12d7360ca04eb192dfbee201d1c4bb116))
* **WebAPI:** Wire up stream downloader selection and fallback in DownloadJob ([102d755](https://github.com/Reaparr/Reaparr/commit/102d75570c32e69465eec4008bea7750cb0b2c78))

# [0.36.0](https://github.com/Reaparr/Reaparr/compare/v0.35.0...v0.36.0) (2026-02-24)


### Bug Fixes

* **WebAPI:** Add download parameter to download URL, this might in some cases fix an empty download response ([#516](https://github.com/Reaparr/Reaparr/issues/516)) ([1eb56b9](https://github.com/Reaparr/Reaparr/commit/1eb56b902d3ac8b1a1c5692daebd49c5dfdb1059))
* **WebAPI:** Add download parameter to download URL, this might in some cases fix an empty download response ([73d4d80](https://github.com/Reaparr/Reaparr/commit/73d4d803e40e8045131ed24389ac02d62c3ba5ed))
* **Web-UI:** Adjust help row label white-space property ([f11e062](https://github.com/Reaparr/Reaparr/commit/f11e06206adbd397219430c6a257dcc6241ebc08))
* **WebAPI:** Align qBittorrent download client integration ([9e14ac8](https://github.com/Reaparr/Reaparr/commit/9e14ac891f28ce2efc0eb6c70dfff3e64db274e8))
* **WebAPI:** Align qBittorrent v2 polling payloads ([87e5404](https://github.com/Reaparr/Reaparr/commit/87e54041b4aa97a0508500c3acb25a7685d2070c))
* **WebAPI:** Allow retrying stuck DownloadFinished and MoveError tasks via Start action ([9ed9977](https://github.com/Reaparr/Reaparr/commit/9ed9977e6c5f58d9b01a9634894f034c9a2a3cd7))
* **WebAPI:** Await SignalR progress callbacks to prevent dropped notifications during library sync ([772dc39](https://github.com/Reaparr/Reaparr/commit/772dc3986921ced94093a8eacd8dc4fe79a47465))
* **Web-UI:** Correct order of title and text in Radarr and Sonarr connection help rows ([0e4bb9b](https://github.com/Reaparr/Reaparr/commit/0e4bb9bcfe4269f89ea37f1a83f9bf96e3daba8f))
* **WebAPI:** Delete download tasks after torrent delete ([23dffc3](https://github.com/Reaparr/Reaparr/commit/23dffc3786215b70d5d4cadaba277fe2b13d6734))
* **BackgroundJobs:** Dispatch CheckQueuedPlexLibraryToSyncCommand when server is offline ([f57ad23](https://github.com/Reaparr/Reaparr/commit/f57ad2391bec3f092af9c183598df336d1e7c9eb))
* **WebAPI:** Ensured the Sonarr/Radarr setup integration uses the ReverseProxy settings and works when Reaparr is behind a reverse proxy ([6e6e287](https://github.com/Reaparr/Reaparr/commit/6e6e2873cd11c664537db152c1f3c94b4645b0f0))
* **WebAPI:** Fix concurrent DbContext threading issue in DownloadClientAuthenticationPreProcessor ([ec44af6](https://github.com/Reaparr/Reaparr/commit/ec44af63fd94500ede344d144dd3ecc9d1209b94))
* **Application:** Fix flaky DownloadWorker retry test due to Polly backoff accumulation ([d16bf1d](https://github.com/Reaparr/Reaparr/commit/d16bf1d2612911dae6e4ff2e78b2a6d78bfb4573))
* **Tests:** Fix flaky SQLite in-memory database teardown in BaseUnitTest ([fe91cc2](https://github.com/Reaparr/Reaparr/commit/fe91cc20053e8fa641ec0eef9b720ffc59b679ea))
* **Web-UI:** fix gap below poster image wrapper when fallback is shown ([d45533e](https://github.com/Reaparr/Reaparr/commit/d45533e63d9f9b9d4f207148e4f131b9d25524bb))
* **Web-UI:** Fix getIsSorted returning true for NoSort state and add mediaOverviewStore unit tests ([7028ca4](https://github.com/Reaparr/Reaparr/commit/7028ca4f433a42fe7d219405b4e41bf213aff4f7))
* **WebAPI:** Fix O(n×m) hang in ToHashKeyDictionary during library sync ([#533](https://github.com/Reaparr/Reaparr/issues/533)) ([76687fc](https://github.com/Reaparr/Reaparr/commit/76687fce3d12bb1c464963b78e207eb61aa0057d))
* **WebAPI:** Fix O(n×m) hang in ToHashKeyDictionary during library sync ([87b6904](https://github.com/Reaparr/Reaparr/commit/87b6904c389a7c7f8f77be7225bbf09122693d73))
* **WebAPI:** Fix SingleOrDefault exception when OR query matches multiple media data rows ([69caa82](https://github.com/Reaparr/Reaparr/commit/69caa8215faa8bb4538f2b6be0113dead8932bcb))
* **Web-UI:** Fix useI18n called in invalid composition context in MediaOverviewStore ([e756260](https://github.com/Reaparr/Reaparr/commit/e756260643a6249879f4d3d07b732b5887d976eb))
* **Web-UI:** Fixed a bug where the scroll index was incorrect for alternatives sorts such as year, updatedAt etc ([525ce3e](https://github.com/Reaparr/Reaparr/commit/525ce3e1a4fb6dcef6db706c4723a6ac0c9cf810))
* **Web-UI:** Fixed a bug where the scroll index was incorrect size ([354de4a](https://github.com/Reaparr/Reaparr/commit/354de4a5de790dff4b428520003097f454cfd409))
* **WebAPI:** Fixed an annoying issue where DownloadTasks would stay stuck on DownloadFinished, now Reaparr will correct itself and finish moving everything ([dbe116a](https://github.com/Reaparr/Reaparr/commit/dbe116a0d85d039cfbd63bbd1a795157731c0c40))
* **WebAPI:** Fixed an annoying issue where Sonarr/Radarr would hold onto an expired cookie which meant it couldn't connect, now it will refresh automatically on expired authentication ([5201c41](https://github.com/Reaparr/Reaparr/commit/5201c414fc814262a2cff1ddc5fd7573178dfd51))
* **WebAPI:** Fixed an issue where the hoster domainname of Reaparr was not forwarder correctly when setting up Sonarr/Radarr ([d0a588b](https://github.com/Reaparr/Reaparr/commit/d0a588bf1fbd49278a252a6ca259c4a50bd04d4f))
* **Web-UI:** Fixed an issue where the Posters would not be centered on the screen depending on the window size ([36f0e5b](https://github.com/Reaparr/Reaparr/commit/36f0e5b2db045fb9272f5b4e281bd40c24d697b0))
* **WebAPI:** Fixed an issue where the syncing of a huge Plex library 100.000+ movies would hang and silently crash ([4bb1d47](https://github.com/Reaparr/Reaparr/commit/4bb1d4734a0e36b760cc2e4081d8e07eec72dedb))
* **Web-UI:** Fixed an visual issue where in Brave/Microsoft Edge the download speed limit and download table would be white instead of the correct color. Also improved the checkboxes on the download table ([f8a3da1](https://github.com/Reaparr/Reaparr/commit/f8a3da1caf4cc41562874f7106b4b29be4a0d6c1))
* **WebAPI:** Fixed downloads through Sonarr/Radarr not kept in the downloads folder ([eb013e7](https://github.com/Reaparr/Reaparr/commit/eb013e7ec9894c8480394de99755957476a01ba1))
* **WebAPI:** Fixed dupplicate changelog description in Discord notifications ([d2a9e71](https://github.com/Reaparr/Reaparr/commit/d2a9e71d0baf051724d886b14d1a543330dc9348))
* **WebAPI:** Fixed incorrect download urls being generated for Radarr/Sonarr, missing the DownloadTorrent path ([4c1818c](https://github.com/Reaparr/Reaparr/commit/4c1818c980d616e726e64bc2fd43618ed267ff2c))
* **WebAPI:** Fixed Reaparr not being able to set the DownloadClient in Sonarr and Radarr ([ab53a06](https://github.com/Reaparr/Reaparr/commit/ab53a06cc15551992719284ec4050874165b6679))
* **WebAPI:** Fixed Sonarr/Radarr not being able to delete or remove a downloadtask in progress ([807c56e](https://github.com/Reaparr/Reaparr/commit/807c56e0d167f598af50a867b7075907b30f8c30))
* **WebAPI:** Fixed the indexer returning results from offline servers, this is now always only online servers ([3e204f8](https://github.com/Reaparr/Reaparr/commit/3e204f8112e857e82b4b3bc3fbaf8ba964e99a44))
* **Web-UI:** Fixed the login screen flashing for a split second after logging in, now the transition is smooth ([ed164e4](https://github.com/Reaparr/Reaparr/commit/ed164e4ee0a4a84b8e5322d15d70ac91f9f298f3))
* **Web-UI:** Fixed the mask server name and mask library names settings not working ([46dc2a2](https://github.com/Reaparr/Reaparr/commit/46dc2a2d117543edce08ff850872cade0158426a))
* **WebAPI:** Fixed the missing intermediate download path when sending progress to Sonarr/Radarr, and corrected the percentage count ([0ae4b04](https://github.com/Reaparr/Reaparr/commit/0ae4b04395094c507e32894fef1deced9577a077))
* **Web-UI:** Fixed the poster table not rendering when there are more than 250.000 posters being loaded ([3c451b1](https://github.com/Reaparr/Reaparr/commit/3c451b1bac4d19f739b5248e8ebbdcaee96370dd))
* **Web-UI:** Fixed the scroll navigation not working for the table view ([6d0e031](https://github.com/Reaparr/Reaparr/commit/6d0e03154666d09dafd8f92bbc4bf38b286c4711))
* **Web-UI:** Fixed the sorting icons in the media table view ([22596d6](https://github.com/Reaparr/Reaparr/commit/22596d68a52452732cdd627b812f868f5980f5b2))
* **WebAPI:** Keep download move queue advancing ([d53e5cb](https://github.com/Reaparr/Reaparr/commit/d53e5cb75d742d50b8a1ca6fe0cc777f78bf7d73))
* **WebAPI:** Possible bug fix for DownloadTask getting stuck on DownloadFinished ([d9c50fd](https://github.com/Reaparr/Reaparr/commit/d9c50fd97fe3f6c6869d234c736974247d146d2c))
* **Web-UI:** Prevent media requests from freezing when rapidly switching libraries ([c2f1070](https://github.com/Reaparr/Reaparr/commit/c2f1070216872b01430f5f80fe83f2b1c143af44))
* **WebAPI:** Propagate CustomDestinationFolderPath through TV show download task hierarchy ([037e5d2](https://github.com/Reaparr/Reaparr/commit/037e5d2241a0a2b8bd62650dfe9883e01986c6b7))
* **Web-UI:** Refresh settings after Sonarr/Radarr integration configure to mark step 2 as done ([38a6782](https://github.com/Reaparr/Reaparr/commit/38a67823bc4613fc4d464c2307440f43610fc01e))
* **WebAPI:** Remove the limitations of 1 file that can be moved at the time, this would lead to a mess of unmoved finished tasks ([43452b0](https://github.com/Reaparr/Reaparr/commit/43452b033a82596b04578fae81ab7cbca38283d7))
* **Web-UI:** remove unused i18n key 'account-card.no-account-name' ([b1109a8](https://github.com/Reaparr/Reaparr/commit/b1109a85ecdd4c1ed59cbfcca97bc48f22903d60))
* **Web-UI:** Show error dialog when Plex token validation fails with invalid/expired token ([c22e908](https://github.com/Reaparr/Reaparr/commit/c22e9080586d540a82c426b211e86eca1b48559a)), closes [#531](https://github.com/Reaparr/Reaparr/issues/531)
* **Web-UI:** Validate username and password before allowing credential updates ([75bcb34](https://github.com/Reaparr/Reaparr/commit/75bcb34b05c140b869ec986e0439ddb0f5e4d0d9))


### Features

* **WebAPI:** Add clear configuration endpoints for Radarr and Sonarr integrations ([801a873](https://github.com/Reaparr/Reaparr/commit/801a873537b804de191aed614461b61713ac4587))
* **Web-UI:** Add FAQ link to username help text in account dialog ([c6a27c8](https://github.com/Reaparr/Reaparr/commit/c6a27c8b79574db366a9bdf3066228160fad66a9))
* **Web-UI:** Add option to mask Plex account names under Settings => General => Mask Account Names ([e4d8f3d](https://github.com/Reaparr/Reaparr/commit/e4d8f3dad2ecebd2a7090cdc18d63b4a06bbb774))
* **WebAPI:** Add request cancellation middleware to suppress client-aborted exceptions ([2abd96f](https://github.com/Reaparr/Reaparr/commit/2abd96f99bb9ba326f2535a8e3ed18ea1ea27908))
* **WebAPI:** Add Reset method to settings modules via IBaseSettingsModule static abstract interface ([6c2526d](https://github.com/Reaparr/Reaparr/commit/6c2526d4b69547f4a36384b05f847f5ee6244df7))
* **Web-UI:** Add sorting functionality to media poster overview ([87f57ed](https://github.com/Reaparr/Reaparr/commit/87f57ed3d1db4f2822efe203aa0615caac9517d5))
* **Web-UI:** Add titles for Sonarr and Radarr integrations title header in multiple languages ([67d46d6](https://github.com/Reaparr/Reaparr/commit/67d46d6b37b1903fd56da6bcda41754c7d0d8cae))
* **Web-UI:** Added a background animation toggle button to the setup page to quickly disable the animated background ([f7a8117](https://github.com/Reaparr/Reaparr/commit/f7a8117516d8f78e4599b6cbf0ac166efbfb862f))
* **Web-UI:** Added a clear config button to the Sonarr and Radarr integration settings page ([0265e13](https://github.com/Reaparr/Reaparr/commit/0265e13ecb6d9cc21a369ff41ff1c5ab05c4425b))
* **Web-UI:** Added a cool effect to the active item in the Library list to show which one is selected now ([d236c6e](https://github.com/Reaparr/Reaparr/commit/d236c6e671e547f5cf64aa3c747d981d87add8b9))
* **WebAPI:** Added a maskAccountNames toggle to the Reaparr settings file ([90053aa](https://github.com/Reaparr/Reaparr/commit/90053aad4526d5b36658ed9a469d1333d15f2b19))
* **WebAPI:** Added a maskAccountNames toggle to the Reaparr settings… ([#520](https://github.com/Reaparr/Reaparr/issues/520)) ([4361839](https://github.com/Reaparr/Reaparr/commit/43618393551c9e08b07c4ad687020485ffada2ba))
* **Web-UI:** Added an info alert under the "Unhide Plex Servers" section when no servers are hidden yet ([8b73bf4](https://github.com/Reaparr/Reaparr/commit/8b73bf4275e200a6fca9f034bbd5cb227ec703b1))
* **WebAPI:** Added an optional stream downloader method which will automatically attempt to download a file when direct file download fails ([5ae2728](https://github.com/Reaparr/Reaparr/commit/5ae272811b55db26227237fba778763e6c362b7a))
* **Web-UI:** Added cancel button to the MediaOverview screen when refreshing ([2d94b82](https://github.com/Reaparr/Reaparr/commit/2d94b823d80e52b692f78152cca3c5b2cb55291e))
* **Web-UI:** Added options under Settings => Advanced => Network and Reverse Proxy to set up Reaparr behind a reverse proxy ([c716df2](https://github.com/Reaparr/Reaparr/commit/c716df253a6967305a9b08ba1c8180efc0877771))
* **WebAPI:** Added reverse proxy settings to the settings storage ([5e4b86b](https://github.com/Reaparr/Reaparr/commit/5e4b86b92f4b36b076e0f04abc36967ea388af81))
* **Web-UI:** Added the sorting to the main media view on the homepage and some cleanup ([7ca7fad](https://github.com/Reaparr/Reaparr/commit/7ca7fad966788077641a811883cc428e76fc5fee))
* **Web-UI:** Converted the MediaTable with just rows to a new table framework ([378efff](https://github.com/Reaparr/Reaparr/commit/378efff9783ccc877e4078b5b40c85e2bc14b326))
* **WebAPI:** Implement DashMpdCliWrapper for MPEG-DASH media downloads ([a3e555d](https://github.com/Reaparr/Reaparr/commit/a3e555d77f24d3ab792320dde91d7eae315e59b0))
* **Web-UI:** Improved the letter navigation on the poster overview page to change depending on what sorting you're using ([86cdc16](https://github.com/Reaparr/Reaparr/commit/86cdc160f565b7c6c1478a21bef94912dd93357b))
* **Web-UI:** Improved the media sync display with more progress bars ([edf1c58](https://github.com/Reaparr/Reaparr/commit/edf1c58a35c7b16ded613c03d0457888d8ba4849))
* **Web-UI:** Moved the MaskServerNames and MaskLibraryNames settings to the UI => General page ([2ea8059](https://github.com/Reaparr/Reaparr/commit/2ea80595cf257e23b7d17b6cda6b819d2d3b01ff))
* **WebAPI:** Support SEQ logging container for an external loggin source ([9e3f23a](https://github.com/Reaparr/Reaparr/commit/9e3f23aad1c9824ef7e2f18d5087cb98c9dbbdbb))
* **WebAPI:** The LibrarySync process can now be cancelled from the MediaOverview refresh screen ([5ef3dee](https://github.com/Reaparr/Reaparr/commit/5ef3dee4fd3ecffbe86f8ee61d493914a2183dac))


### Performance Improvements

* **Web-UI:** Massive performance improvement on rendering the media posters in the media overview. This is especially noticeable on the homepage! ([5c21b3a](https://github.com/Reaparr/Reaparr/commit/5c21b3ad915c29aec62214685f211ad9d83d7047))
* **WebAPI:** Remove PlexToken from Media DTOs, this should save some bandwith and make navigating media faster ([924246b](https://github.com/Reaparr/Reaparr/commit/924246beb2bd75e1eecf95fd67ea070366ac7d8d))

# [0.36.0-dev.7](https://github.com/Reaparr/Reaparr/compare/v0.36.0-dev.6...v0.36.0-dev.7) (2026-02-24)


### Bug Fixes

* **WebAPI:** Align qBittorrent download client integration ([9e14ac8](https://github.com/Reaparr/Reaparr/commit/9e14ac891f28ce2efc0eb6c70dfff3e64db274e8))
* **WebAPI:** Align qBittorrent v2 polling payloads ([87e5404](https://github.com/Reaparr/Reaparr/commit/87e54041b4aa97a0508500c3acb25a7685d2070c))
* **WebAPI:** Delete download tasks after torrent delete ([23dffc3](https://github.com/Reaparr/Reaparr/commit/23dffc3786215b70d5d4cadaba277fe2b13d6734))
* **WebAPI:** Ensured the Sonarr/Radarr setup integration uses the ReverseProxy settings and works when Reaparr is behind a reverse proxy ([6e6e287](https://github.com/Reaparr/Reaparr/commit/6e6e2873cd11c664537db152c1f3c94b4645b0f0))
* **WebAPI:** Fixed an annoying issue where DownloadTasks would stay stuck on DownloadFinished, now Reaparr will correct itself and finish moving everything ([dbe116a](https://github.com/Reaparr/Reaparr/commit/dbe116a0d85d039cfbd63bbd1a795157731c0c40))
* **WebAPI:** Fixed an annoying issue where Sonarr/Radarr would hold onto an expired cookie which meant it couldn't connect, now it will refresh automatically on expired authentication ([5201c41](https://github.com/Reaparr/Reaparr/commit/5201c414fc814262a2cff1ddc5fd7573178dfd51))
* **WebAPI:** Fixed an issue where the hoster domainname of Reaparr was not forwarder correctly when setting up Sonarr/Radarr ([d0a588b](https://github.com/Reaparr/Reaparr/commit/d0a588bf1fbd49278a252a6ca259c4a50bd04d4f))
* **WebAPI:** Fixed downloads through Sonarr/Radarr not kept in the downloads folder ([eb013e7](https://github.com/Reaparr/Reaparr/commit/eb013e7ec9894c8480394de99755957476a01ba1))
* **WebAPI:** Fixed incorrect download urls being generated for Radarr/Sonarr, missing the DownloadTorrent path ([4c1818c](https://github.com/Reaparr/Reaparr/commit/4c1818c980d616e726e64bc2fd43618ed267ff2c))
* **WebAPI:** Fixed Sonarr/Radarr not being able to delete or remove a downloadtask in progress ([807c56e](https://github.com/Reaparr/Reaparr/commit/807c56e0d167f598af50a867b7075907b30f8c30))
* **WebAPI:** Fixed the indexer returning results from offline servers, this is now always only online servers ([3e204f8](https://github.com/Reaparr/Reaparr/commit/3e204f8112e857e82b4b3bc3fbaf8ba964e99a44))
* **WebAPI:** Fixed the missing intermediate download path when sending progress to Sonarr/Radarr, and corrected the percentage count ([0ae4b04](https://github.com/Reaparr/Reaparr/commit/0ae4b04395094c507e32894fef1deced9577a077))
* **WebAPI:** Keep download move queue advancing ([d53e5cb](https://github.com/Reaparr/Reaparr/commit/d53e5cb75d742d50b8a1ca6fe0cc777f78bf7d73))


### Features

* **Web-UI:** Added options under Settings => Advanced => Network and Reverse Proxy to set up Reaparr behind a reverse proxy ([c716df2](https://github.com/Reaparr/Reaparr/commit/c716df253a6967305a9b08ba1c8180efc0877771))
* **WebAPI:** Added reverse proxy settings to the settings storage ([5e4b86b](https://github.com/Reaparr/Reaparr/commit/5e4b86b92f4b36b076e0f04abc36967ea388af81))

# [0.36.0-dev.6](https://github.com/Reaparr/Reaparr/compare/v0.36.0-dev.5...v0.36.0-dev.6) (2026-02-23)


### Bug Fixes

* **WebAPI:** Fix concurrent DbContext threading issue in DownloadClientAuthenticationPreProcessor ([ec44af6](https://github.com/Reaparr/Reaparr/commit/ec44af63fd94500ede344d144dd3ecc9d1209b94))
* **Tests:** Fix flaky SQLite in-memory database teardown in BaseUnitTest ([fe91cc2](https://github.com/Reaparr/Reaparr/commit/fe91cc20053e8fa641ec0eef9b720ffc59b679ea))
* **WebAPI:** Fix SingleOrDefault exception when OR query matches multiple media data rows ([69caa82](https://github.com/Reaparr/Reaparr/commit/69caa8215faa8bb4538f2b6be0113dead8932bcb))
* **WebAPI:** Fixed Reaparr not being able to set the DownloadClient in Sonarr and Radarr ([ab53a06](https://github.com/Reaparr/Reaparr/commit/ab53a06cc15551992719284ec4050874165b6679))
* **Web-UI:** Refresh settings after Sonarr/Radarr integration configure to mark step 2 as done ([38a6782](https://github.com/Reaparr/Reaparr/commit/38a67823bc4613fc4d464c2307440f43610fc01e))
* **WebAPI:** Remove the limitations of 1 file that can be moved at the time, this would lead to a mess of unmoved finished tasks ([43452b0](https://github.com/Reaparr/Reaparr/commit/43452b033a82596b04578fae81ab7cbca38283d7))


### Features

* **WebAPI:** Support SEQ logging container for an external loggin source ([9e3f23a](https://github.com/Reaparr/Reaparr/commit/9e3f23aad1c9824ef7e2f18d5087cb98c9dbbdbb))

# [0.36.0-dev.5](https://github.com/Reaparr/Reaparr/compare/v0.36.0-dev.4...v0.36.0-dev.5) (2026-02-22)


### Bug Fixes

* **BackgroundJobs:** Dispatch CheckQueuedPlexLibraryToSyncCommand when server is offline ([f57ad23](https://github.com/Reaparr/Reaparr/commit/f57ad2391bec3f092af9c183598df336d1e7c9eb))
* **Application:** Fix flaky DownloadWorker retry test due to Polly backoff accumulation ([d16bf1d](https://github.com/Reaparr/Reaparr/commit/d16bf1d2612911dae6e4ff2e78b2a6d78bfb4573))
* **WebAPI:** Fix O(n×m) hang in ToHashKeyDictionary during library sync ([#533](https://github.com/Reaparr/Reaparr/issues/533)) ([76687fc](https://github.com/Reaparr/Reaparr/commit/76687fce3d12bb1c464963b78e207eb61aa0057d))
* **WebAPI:** Fix O(n×m) hang in ToHashKeyDictionary during library sync ([87b6904](https://github.com/Reaparr/Reaparr/commit/87b6904c389a7c7f8f77be7225bbf09122693d73))


### Features

* **Web-UI:** Added cancel button to the MediaOverview screen when refreshing ([2d94b82](https://github.com/Reaparr/Reaparr/commit/2d94b823d80e52b692f78152cca3c5b2cb55291e))
* **WebAPI:** The LibrarySync process can now be cancelled from the MediaOverview refresh screen ([5ef3dee](https://github.com/Reaparr/Reaparr/commit/5ef3dee4fd3ecffbe86f8ee61d493914a2183dac))

# [0.36.0-dev.4](https://github.com/Reaparr/Reaparr/compare/v0.36.0-dev.3...v0.36.0-dev.4) (2026-02-21)


### Bug Fixes

* **WebAPI:** Allow retrying stuck DownloadFinished and MoveError tasks via Start action ([9ed9977](https://github.com/Reaparr/Reaparr/commit/9ed9977e6c5f58d9b01a9634894f034c9a2a3cd7))
* **Web-UI:** Fix useI18n called in invalid composition context in MediaOverviewStore ([e756260](https://github.com/Reaparr/Reaparr/commit/e756260643a6249879f4d3d07b732b5887d976eb))
* **Web-UI:** Fixed a bug where the scroll index was incorrect for alternatives sorts such as year, updatedAt etc ([525ce3e](https://github.com/Reaparr/Reaparr/commit/525ce3e1a4fb6dcef6db706c4723a6ac0c9cf810))
* **Web-UI:** Fixed a bug where the scroll index was incorrect size ([354de4a](https://github.com/Reaparr/Reaparr/commit/354de4a5de790dff4b428520003097f454cfd409))
* **Web-UI:** Fixed an issue where the Posters would not be centered on the screen depending on the window size ([36f0e5b](https://github.com/Reaparr/Reaparr/commit/36f0e5b2db045fb9272f5b4e281bd40c24d697b0))
* **WebAPI:** Fixed an issue where the syncing of a huge Plex library 100.000+ movies would hang and silently crash ([4bb1d47](https://github.com/Reaparr/Reaparr/commit/4bb1d4734a0e36b760cc2e4081d8e07eec72dedb))
* **Web-UI:** Fixed the poster table not rendering when there are more than 250.000 posters being loaded ([3c451b1](https://github.com/Reaparr/Reaparr/commit/3c451b1bac4d19f739b5248e8ebbdcaee96370dd))
* **Web-UI:** Fixed the scroll navigation not working for the table view ([6d0e031](https://github.com/Reaparr/Reaparr/commit/6d0e03154666d09dafd8f92bbc4bf38b286c4711))
* **Web-UI:** Fixed the sorting icons in the media table view ([22596d6](https://github.com/Reaparr/Reaparr/commit/22596d68a52452732cdd627b812f868f5980f5b2))
* **WebAPI:** Possible bug fix for DownloadTask getting stuck on DownloadFinished ([d9c50fd](https://github.com/Reaparr/Reaparr/commit/d9c50fd97fe3f6c6869d234c736974247d146d2c))
* **Web-UI:** Prevent media requests from freezing when rapidly switching libraries ([c2f1070](https://github.com/Reaparr/Reaparr/commit/c2f1070216872b01430f5f80fe83f2b1c143af44))
* **WebAPI:** Propagate CustomDestinationFolderPath through TV show download task hierarchy ([037e5d2](https://github.com/Reaparr/Reaparr/commit/037e5d2241a0a2b8bd62650dfe9883e01986c6b7))
* **Web-UI:** Show error dialog when Plex token validation fails with invalid/expired token ([c22e908](https://github.com/Reaparr/Reaparr/commit/c22e9080586d540a82c426b211e86eca1b48559a)), closes [#531](https://github.com/Reaparr/Reaparr/issues/531)


### Features

* **WebAPI:** Add request cancellation middleware to suppress client-aborted exceptions ([2abd96f](https://github.com/Reaparr/Reaparr/commit/2abd96f99bb9ba326f2535a8e3ed18ea1ea27908))
* **Web-UI:** Added a cool effect to the active item in the Library list to show which one is selected now ([d236c6e](https://github.com/Reaparr/Reaparr/commit/d236c6e671e547f5cf64aa3c747d981d87add8b9))
* **Web-UI:** Converted the MediaTable with just rows to a new table framework ([378efff](https://github.com/Reaparr/Reaparr/commit/378efff9783ccc877e4078b5b40c85e2bc14b326))


### Performance Improvements

* **Web-UI:** Massive performance improvement on rendering the media posters in the media overview. This is especially noticeable on the homepage! ([5c21b3a](https://github.com/Reaparr/Reaparr/commit/5c21b3ad915c29aec62214685f211ad9d83d7047))

# [0.36.0-dev.3](https://github.com/Reaparr/Reaparr/compare/v0.36.0-dev.2...v0.36.0-dev.3) (2026-02-19)


### Bug Fixes

* **Web-UI:** Adjust help row label white-space property ([f11e062](https://github.com/Reaparr/Reaparr/commit/f11e06206adbd397219430c6a257dcc6241ebc08))
* **Web-UI:** fix gap below poster image wrapper when fallback is shown ([d45533e](https://github.com/Reaparr/Reaparr/commit/d45533e63d9f9b9d4f207148e4f131b9d25524bb))
* **Web-UI:** remove unused i18n key 'account-card.no-account-name' ([b1109a8](https://github.com/Reaparr/Reaparr/commit/b1109a85ecdd4c1ed59cbfcca97bc48f22903d60))


### Features

* **Web-UI:** Add FAQ link to username help text in account dialog ([c6a27c8](https://github.com/Reaparr/Reaparr/commit/c6a27c8b79574db366a9bdf3066228160fad66a9))
* **Web-UI:** Add option to mask Plex account names under Settings => General => Mask Account Names ([e4d8f3d](https://github.com/Reaparr/Reaparr/commit/e4d8f3dad2ecebd2a7090cdc18d63b4a06bbb774))
* **Web-UI:** Add titles for Sonarr and Radarr integrations title header in multiple languages ([67d46d6](https://github.com/Reaparr/Reaparr/commit/67d46d6b37b1903fd56da6bcda41754c7d0d8cae))
* **WebAPI:** Added a maskAccountNames toggle to the Reaparr settings file ([90053aa](https://github.com/Reaparr/Reaparr/commit/90053aad4526d5b36658ed9a469d1333d15f2b19))
* **WebAPI:** Added a maskAccountNames toggle to the Reaparr settings… ([#520](https://github.com/Reaparr/Reaparr/issues/520)) ([4361839](https://github.com/Reaparr/Reaparr/commit/43618393551c9e08b07c4ad687020485ffada2ba))

# [0.36.0-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.36.0-dev.1...v0.36.0-dev.2) (2026-02-14)


### Bug Fixes

* **WebAPI:** Await SignalR progress callbacks to prevent dropped notifications during library sync ([772dc39](https://github.com/Reaparr/Reaparr/commit/772dc3986921ced94093a8eacd8dc4fe79a47465))
* **Web-UI:** Correct order of title and text in Radarr and Sonarr connection help rows ([0e4bb9b](https://github.com/Reaparr/Reaparr/commit/0e4bb9bcfe4269f89ea37f1a83f9bf96e3daba8f))
* **Web-UI:** Fix getIsSorted returning true for NoSort state and add mediaOverviewStore unit tests ([7028ca4](https://github.com/Reaparr/Reaparr/commit/7028ca4f433a42fe7d219405b4e41bf213aff4f7))
* **Web-UI:** Fixed an visual issue where in Brave/Microsoft Edge the download speed limit and download table would be white instead of the correct color. Also improved the checkboxes on the download table ([f8a3da1](https://github.com/Reaparr/Reaparr/commit/f8a3da1caf4cc41562874f7106b4b29be4a0d6c1))
* **WebAPI:** Fixed dupplicate changelog description in Discord notifications ([d2a9e71](https://github.com/Reaparr/Reaparr/commit/d2a9e71d0baf051724d886b14d1a543330dc9348))
* **Web-UI:** Fixed the login screen flashing for a split second after logging in, now the transition is smooth ([ed164e4](https://github.com/Reaparr/Reaparr/commit/ed164e4ee0a4a84b8e5322d15d70ac91f9f298f3))
* **Web-UI:** Fixed the mask server name and mask library names settings not working ([46dc2a2](https://github.com/Reaparr/Reaparr/commit/46dc2a2d117543edce08ff850872cade0158426a))
* **Web-UI:** Validate username and password before allowing credential updates ([75bcb34](https://github.com/Reaparr/Reaparr/commit/75bcb34b05c140b869ec986e0439ddb0f5e4d0d9))


### Features

* **WebAPI:** Add clear configuration endpoints for Radarr and Sonarr integrations ([801a873](https://github.com/Reaparr/Reaparr/commit/801a873537b804de191aed614461b61713ac4587))
* **WebAPI:** Add Reset method to settings modules via IBaseSettingsModule static abstract interface ([6c2526d](https://github.com/Reaparr/Reaparr/commit/6c2526d4b69547f4a36384b05f847f5ee6244df7))
* **Web-UI:** Add sorting functionality to media poster overview ([87f57ed](https://github.com/Reaparr/Reaparr/commit/87f57ed3d1db4f2822efe203aa0615caac9517d5))
* **Web-UI:** Added a background animation toggle button to the setup page to quickly disable the animated background ([f7a8117](https://github.com/Reaparr/Reaparr/commit/f7a8117516d8f78e4599b6cbf0ac166efbfb862f))
* **Web-UI:** Added a clear config button to the Sonarr and Radarr integration settings page ([0265e13](https://github.com/Reaparr/Reaparr/commit/0265e13ecb6d9cc21a369ff41ff1c5ab05c4425b))
* **Web-UI:** Added an info alert under the "Unhide Plex Servers" section when no servers are hidden yet ([8b73bf4](https://github.com/Reaparr/Reaparr/commit/8b73bf4275e200a6fca9f034bbd5cb227ec703b1))
* **Web-UI:** Added the sorting to the main media view on the homepage and some cleanup ([7ca7fad](https://github.com/Reaparr/Reaparr/commit/7ca7fad966788077641a811883cc428e76fc5fee))
* **Web-UI:** Improved the letter navigation on the poster overview page to change depending on what sorting you're using ([86cdc16](https://github.com/Reaparr/Reaparr/commit/86cdc160f565b7c6c1478a21bef94912dd93357b))
* **Web-UI:** Improved the media sync display with more progress bars ([edf1c58](https://github.com/Reaparr/Reaparr/commit/edf1c58a35c7b16ded613c03d0457888d8ba4849))
* **Web-UI:** Moved the MaskServerNames and MaskLibraryNames settings to the UI => General page ([2ea8059](https://github.com/Reaparr/Reaparr/commit/2ea80595cf257e23b7d17b6cda6b819d2d3b01ff))


### Performance Improvements

* **WebAPI:** Remove PlexToken from Media DTOs, this should save some bandwith and make navigating media faster ([924246b](https://github.com/Reaparr/Reaparr/commit/924246beb2bd75e1eecf95fd67ea070366ac7d8d))

# [0.36.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.35.1-dev.1...v0.36.0-dev.1) (2026-02-11)


### Features

* **WebAPI:** Added an optional stream downloader method which will automatically attempt to download a file when direct file download fails ([5ae2728](https://github.com/Reaparr/Reaparr/commit/5ae272811b55db26227237fba778763e6c362b7a))
* **WebAPI:** Implement DashMpdCliWrapper for MPEG-DASH media downloads ([a3e555d](https://github.com/Reaparr/Reaparr/commit/a3e555d77f24d3ab792320dde91d7eae315e59b0))

## [0.35.1-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.35.0...v0.35.1-dev.1) (2026-01-24)


### Bug Fixes

* **WebAPI:** Add download parameter to download URL, this might in some cases fix an empty download response ([#516](https://github.com/Reaparr/Reaparr/issues/516)) ([1eb56b9](https://github.com/Reaparr/Reaparr/commit/1eb56b902d3ac8b1a1c5692daebd49c5dfdb1059))
* **WebAPI:** Add download parameter to download URL, this might in some cases fix an empty download response ([73d4d80](https://github.com/Reaparr/Reaparr/commit/73d4d803e40e8045131ed24389ac02d62c3ba5ed))

# [0.35.0](https://github.com/Reaparr/Reaparr/compare/v0.34.0...v0.35.0) (2026-01-20)


### Bug Fixes

* **WebAPI:** Add timeout protection to prevent indefinite blocking on pause operations ([0b24bbf](https://github.com/Reaparr/Reaparr/commit/0b24bbf39ebb5972f3f55bea40c8087aaeba1d69))
* **WebAPI:** Added a temporary fix for validating an account token, i… ([#500](https://github.com/Reaparr/Reaparr/issues/500)) ([c22efe1](https://github.com/Reaparr/Reaparr/commit/c22efe1dda7aff2813140a9ba235d4133f4cb973))
* **WebAPI:** Added a temporary fix for validating an account token, it requires a larger Plex API SDK update ([afb6778](https://github.com/Reaparr/Reaparr/commit/afb6778f2a86570871a429be87ac201c39299843))
* **WebAPI:** Avoid failing when cleanup directory has files ([06d1820](https://github.com/Reaparr/Reaparr/commit/06d1820a06d1baca3a4b8e4aff7e5779a67920db))
* **Web-UI:** Disable the test connection button if the input fields are invalid ([e111548](https://github.com/Reaparr/Reaparr/commit/e111548521332b7b9bc22dfb0db2bff0bc7d9d77))
* **deps:** downgrade [@vueuse](https://github.com/vueuse) packages to version 13.x ([9e0c95a](https://github.com/Reaparr/Reaparr/commit/9e0c95a9227a9c5471cc6c3adf9861d27ca1dd97))
* **Web-UI:** Fix flashy UI changes when refreshing a library ([3c5ea7b](https://github.com/Reaparr/Reaparr/commit/3c5ea7bbe25682ef03b34b0384806d89ecae6d1a))
* **WebAPI:** Fix threading issues with the dbcontext in handlers ([372b922](https://github.com/Reaparr/Reaparr/commit/372b92241c3d52419531ad81fdd6c4ce411a8478))
* **WebAPI:** Fixed a bug where an auth token would validate but return a 400 bad request ([0b24793](https://github.com/Reaparr/Reaparr/commit/0b247932c3fc961cadc2960afedfddab9a77b976))
* **WebAPI:** Fixed an exception where during the progres update of a file transfer it would crash due to multiple threads using a DbContext ([e54a27f](https://github.com/Reaparr/Reaparr/commit/e54a27f78ad2e0b20e23e022cf62a523a6356054))
* **Web-UI:** Fixed an issue where logging out would redirect to the setup page ([ce3423a](https://github.com/Reaparr/Reaparr/commit/ce3423adbb706f4dca9ee09c66f72e71f34309bf))
* **Web-UI:** Fixed an issue where some alerts had missing colors ([19b9850](https://github.com/Reaparr/Reaparr/commit/19b9850c5c70cef6ac03e4765d7243b7ebace411))
* **WebAPI:** Fixed an issue where the library sync progress would go above 100% ([df213b8](https://github.com/Reaparr/Reaparr/commit/df213b8302e960c8934eb688d9db877f55e268b4))
* **Web-UI:** Fixed an issue where the poster image title would go out of bounds when to long ([1c99c16](https://github.com/Reaparr/Reaparr/commit/1c99c16282f799012f77b6040e41ba410f19c8b7))
* **Web-UI:** Fixed an issue where the poster image would not show the fallback when loading fails ([e45393d](https://github.com/Reaparr/Reaparr/commit/e45393ddd57de0e20643de6b7d6a416f42fdc64b))
* **Web-UI:** Fixed an issue where the poster image would repeat after scrolling far enough on the wrong titles ([5170a9e](https://github.com/Reaparr/Reaparr/commit/5170a9eb3bf8b0c0fef04431cea9588d25f44abc))
* **Web-UI:** Fixed an issue where you could not save a Plex account by using a token ([af1aa56](https://github.com/Reaparr/Reaparr/commit/af1aa563ec169740a183700c4cde926a651c70b8))
* **WebAPI:** Fixed crash issues due to incorrect multi-threaded use of the dbcontext ([82727ab](https://github.com/Reaparr/Reaparr/commit/82727ab7a5656488b05e66cf7b7bbde1e1b17798))
* **Web-UI:** Fixed the confirmation button on the delete account dialog not showing the loading icon when it's busy ([ec9ed9f](https://github.com/Reaparr/Reaparr/commit/ec9ed9f3e94cb8063db80d906cde88f6fc76a37f))
* **WebAPI:** Fixed the discord message containing the changelog be du… ([#515](https://github.com/Reaparr/Reaparr/issues/515)) ([c78475f](https://github.com/Reaparr/Reaparr/commit/c78475fca28366e4eb1d1f40254f443fe5bfe669))
* **WebAPI:** Fixed the discord message containing the changelog be dupplicated ([a0f1a0f](https://github.com/Reaparr/Reaparr/commit/a0f1a0fa1ecec75063c47de2e9ffb34bcd01dacb))
* **WebAPI:** Fixed the SyncServerMedia command not working to sync all media with a server ([2f624f4](https://github.com/Reaparr/Reaparr/commit/2f624f411382c9d160d4705d80bc662e3d486b8c))
* **WebAPI:** Improve error handling for Plex thumbnail fetching ([2b6b677](https://github.com/Reaparr/Reaparr/commit/2b6b677c190e3c199d5ade79a1953b127117c869))
* **WebAPI:** Increase HTTP client timeout for thumbnail image requests ([e70bfe3](https://github.com/Reaparr/Reaparr/commit/e70bfe3e2703938a630bcc5cf4a5d6504d5eb33b))
* **Web-UI:** Open on the correct auth tab and disable switching in edit mode in the Account Dialog ([81950ab](https://github.com/Reaparr/Reaparr/commit/81950abb2e86a324377e568bb3155869b0a4b2fc))
* **WebAPI:** Possible fix for downloads getting stuck and not progressing due to race conditions ([ee9ab02](https://github.com/Reaparr/Reaparr/commit/ee9ab02f25668eb5972806f5bb0233bc443df13a))
* **WebAPI:** Remove duplicate changelog entries ([c3e6551](https://github.com/Reaparr/Reaparr/commit/c3e6551547edb35bf889459a4f9938f6cd8063ca))
* **WebAPI:** Remove USER directive to fix s6-overlay ARM startup error ([b5f3ce1](https://github.com/Reaparr/Reaparr/commit/b5f3ce1eec1f25d0273ee46f4e7f52e56624634b))
* **WebAPI:** resolve memory leak in download client disposal ([a99cdae](https://github.com/Reaparr/Reaparr/commit/a99cdae13f5669e4b7dbc31a44199c119cfe87cf))
* **WebAPI:** resolve SQLite ConnectionError[20004] during concurrent database access ([eb916e2](https://github.com/Reaparr/Reaparr/commit/eb916e220b01cb48700227a4171ae9a6c98fb673))
* **WebAPI:** use split queries to prevent cartesian explosion in collection includes ([d859772](https://github.com/Reaparr/Reaparr/commit/d8597721ff1c2bbb7286979654262ccaa6a2f0de))


### Features

* **WebAPI:** add dev prerelease support with semantic-release ([bb9b9ed](https://github.com/Reaparr/Reaparr/commit/bb9b9edc93a0a1786eb9020a7dbea4134a322a81))
* **WebAPI:** Add media filename validation extension methods to ensure Sonarr/Radarr can parse filenames correctly ([649420f](https://github.com/Reaparr/Reaparr/commit/649420f8fb5b66d15ce58fbac9865e62a34ee689))
* **Web-UI:** Add Radarr integration setup and connection features ([014ef2d](https://github.com/Reaparr/Reaparr/commit/014ef2d98e752696b24262787b2ef8262afad94d))
* **WebAPI:** Added apiKey authentication to the torznab indexer ([2ee560e](https://github.com/Reaparr/Reaparr/commit/2ee560e7d70b53ae25fcdcf6b403d8a1e7d458aa))
* **WebAPI:** Added changelog discord notification ([#508](https://github.com/Reaparr/Reaparr/issues/508)) ([1e0c69d](https://github.com/Reaparr/Reaparr/commit/1e0c69d61e1d856f2be2d2dfe31b722e91b6c723))
* **WebAPI:** Added changelog discord notification ([a73a0db](https://github.com/Reaparr/Reaparr/commit/a73a0db67db531af16c05f5adb02c4da34035f52))
* **Web-UI:** Added error handling when Sonarr/Radarr is not connectable ([26cc448](https://github.com/Reaparr/Reaparr/commit/26cc448c9f7ab7428be21dd3c0b1b57a6c403786))
* **WebAPI:** Added session based authentication to the Downloadclient that mimics QBittorrent ([904dfd1](https://github.com/Reaparr/Reaparr/commit/904dfd1ea92b57401f2f694a15051df1d20a06cc))
* **Web-UI:** Allow markdown to be rendered in the help dialog boxes to improve formatting and adding links ([b98b946](https://github.com/Reaparr/Reaparr/commit/b98b9464f607390e9dbfcad08a710423fe923582))
* **WebAPI:** ARM Support to auto build and publish in the pipeline ([377aed4](https://github.com/Reaparr/Reaparr/commit/377aed41e09989e0d8a18dda7cdbf7bec76b5391))
* **WebAPI:** Implement persistent auth session management for DownloadClient ([fba5e53](https://github.com/Reaparr/Reaparr/commit/fba5e5399637bc04984ddb0db8237bd842f3b5ae))
* **Web-UI:** Links in help text now open a new tab ([7309d2b](https://github.com/Reaparr/Reaparr/commit/7309d2bafff6bae4ac2d918e6f07cc2d7b375ff9))
* **Web-UI:** QOL: When executing Resync Server command, it will no close the dialog and open the correct sync dialog ([abd6703](https://github.com/Reaparr/Reaparr/commit/abd6703e378fb316c619a18e9e82f4f97f41e0dc))
* **Web-UI:** Removed the thick red bar around the media overview bar to a sleek small gray line to be consistent with the rest of the look ([5221d26](https://github.com/Reaparr/Reaparr/commit/5221d26cc028fa225d3974cba577f580ce705e2f))
* **Web-UI:** Sonarr can now be fully automatically configured from the integration page! ([e0da625](https://github.com/Reaparr/Reaparr/commit/e0da6252fcd4f3285ffb83311235210de1d1eb59))
* **WebAPI:** Upgrade target framework to net10.0 across projects ([85d0801](https://github.com/Reaparr/Reaparr/commit/85d0801aba97ad4095c7ff339e08464ea5666a09))


### Performance Improvements

* **WebAPI:** Add indexes for SearchTitle to enhance query performance ([9770486](https://github.com/Reaparr/Reaparr/commit/9770486ecd6ee15919a6b919de89b7d9357d9109))
* **WebAPI:** Add indexes to improve query performance for download tasks ([d5165d4](https://github.com/Reaparr/Reaparr/commit/d5165d4be584e17666a3a935c4639e1d0bdffce2))
* **Environment:** add IsDevelopmentEnvironment helper and enable sensitive data logging in dev/test ([03afac0](https://github.com/Reaparr/Reaparr/commit/03afac0c201a6276f5b7b0e59eee47589a35a472))
* **Web-UI:** Added an optimization of using the GPU when scrolling fast across media posters ([e62b641](https://github.com/Reaparr/Reaparr/commit/e62b641f0ebc4b91ddfd3b9c8029e917d0ffa0a4))
* **WebAPI:** Added more indexes to the database for heavy queries ([d3326c6](https://github.com/Reaparr/Reaparr/commit/d3326c60e4b9eb92f451decb8ed05c67d9923e45))
* **WebAPI:** Hugely improved the performance of loading thumbnail for media posters, this will make navigating much smoother ([3e86fdd](https://github.com/Reaparr/Reaparr/commit/3e86fdd602e03f423ee41073f2b0f7657e38efc4))
* **WebAPI:** Increased database query and storage performance by adding indexes and storing less at the same time ([7aa2559](https://github.com/Reaparr/Reaparr/commit/7aa2559f3176e20433a92c36b44b3fc457aae1ef))
* **WebAPI:** Major rewrite in the way movie and episode data is stored, which should save a lot of database filesize ([3f1c5e5](https://github.com/Reaparr/Reaparr/commit/3f1c5e5c7ae53b6a466c1f6776701dd56b8fdaa4))


### Reverts

* Revert "build(WebAPI): Revert last dev EF migrations" ([8f41e18](https://github.com/Reaparr/Reaparr/commit/8f41e181af2331a367d3d1429f68aaf887b71d7d))
* Revert "refactor(WebAPI): Add LibrarySyncJobQueue migration and update model snapshot" ([e4770d6](https://github.com/Reaparr/Reaparr/commit/e4770d6c4dbae582be9fc8ab62ee312fe096055f))

# [0.35.0-dev.5](https://github.com/Reaparr/Reaparr/compare/v0.35.0-dev.4...v0.35.0-dev.5) (2026-01-20)


### Bug Fixes

* **Web-UI:** Disable the test connection button if the input fields are invalid ([e111548](https://github.com/Reaparr/Reaparr/commit/e111548521332b7b9bc22dfb0db2bff0bc7d9d77))
* **Web-UI:** Fixed an issue where logging out would redirect to the setup page ([ce3423a](https://github.com/Reaparr/Reaparr/commit/ce3423adbb706f4dca9ee09c66f72e71f34309bf))
* **WebAPI:** Fixed the discord message containing the changelog be du… ([#515](https://github.com/Reaparr/Reaparr/issues/515)) ([c78475f](https://github.com/Reaparr/Reaparr/commit/c78475fca28366e4eb1d1f40254f443fe5bfe669))
* **WebAPI:** Fixed the discord message containing the changelog be dupplicated ([a0f1a0f](https://github.com/Reaparr/Reaparr/commit/a0f1a0fa1ecec75063c47de2e9ffb34bcd01dacb))
* **WebAPI:** Improve error handling for Plex thumbnail fetching ([2b6b677](https://github.com/Reaparr/Reaparr/commit/2b6b677c190e3c199d5ade79a1953b127117c869))
* **WebAPI:** Possible fix for downloads getting stuck and not progressing due to race conditions ([ee9ab02](https://github.com/Reaparr/Reaparr/commit/ee9ab02f25668eb5972806f5bb0233bc443df13a))
* **WebAPI:** Remove duplicate changelog entries ([c3e6551](https://github.com/Reaparr/Reaparr/commit/c3e6551547edb35bf889459a4f9938f6cd8063ca))


### Features

* **Web-UI:** Added error handling when Sonarr/Radarr is not connectable ([26cc448](https://github.com/Reaparr/Reaparr/commit/26cc448c9f7ab7428be21dd3c0b1b57a6c403786))

# [0.35.0-dev.4](https://github.com/Reaparr/Reaparr/compare/v0.35.0-dev.3...v0.35.0-dev.4) (2026-01-12)


### Bug Fixes

* **WebAPI:** Remove USER directive to fix s6-overlay ARM startup error ([b5f3ce1](https://github.com/Reaparr/Reaparr/commit/b5f3ce1eec1f25d0273ee46f4e7f52e56624634b))

# [0.35.0-dev.3](https://github.com/Reaparr/Reaparr/compare/v0.35.0-dev.2...v0.35.0-dev.3) (2026-01-11)


### Features

* **WebAPI:** ARM Support to auto build and publish in the pipeline ([377aed4](https://github.com/Reaparr/Reaparr/commit/377aed41e09989e0d8a18dda7cdbf7bec76b5391))

# [0.35.0-dev.2](https://github.com/Reaparr/Reaparr/compare/v0.35.0-dev.1...v0.35.0-dev.2) (2026-01-11)


### Bug Fixes

* **WebAPI:** Add timeout protection to prevent indefinite blocking on pause operations ([0b24bbf](https://github.com/Reaparr/Reaparr/commit/0b24bbf39ebb5972f3f55bea40c8087aaeba1d69))
* **WebAPI:** Avoid failing when cleanup directory has files ([06d1820](https://github.com/Reaparr/Reaparr/commit/06d1820a06d1baca3a4b8e4aff7e5779a67920db))
* **WebAPI:** Fixed the SyncServerMedia command not working to sync all media with a server ([2f624f4](https://github.com/Reaparr/Reaparr/commit/2f624f411382c9d160d4705d80bc662e3d486b8c))


### Features

* **WebAPI:** Add media filename validation extension methods to ensure Sonarr/Radarr can parse filenames correctly ([649420f](https://github.com/Reaparr/Reaparr/commit/649420f8fb5b66d15ce58fbac9865e62a34ee689))
* **WebAPI:** Upgrade target framework to net10.0 across projects ([85d0801](https://github.com/Reaparr/Reaparr/commit/85d0801aba97ad4095c7ff339e08464ea5666a09))


### Performance Improvements

* **WebAPI:** Add indexes for SearchTitle to enhance query performance ([9770486](https://github.com/Reaparr/Reaparr/commit/9770486ecd6ee15919a6b919de89b7d9357d9109))
* **WebAPI:** Add indexes to improve query performance for download tasks ([d5165d4](https://github.com/Reaparr/Reaparr/commit/d5165d4be584e17666a3a935c4639e1d0bdffce2))
* **WebAPI:** Added more indexes to the database for heavy queries ([d3326c6](https://github.com/Reaparr/Reaparr/commit/d3326c60e4b9eb92f451decb8ed05c67d9923e45))
* **WebAPI:** Increased database query and storage performance by adding indexes and storing less at the same time ([7aa2559](https://github.com/Reaparr/Reaparr/commit/7aa2559f3176e20433a92c36b44b3fc457aae1ef))
* **WebAPI:** Major rewrite in the way movie and episode data is stored, which should save a lot of database filesize ([3f1c5e5](https://github.com/Reaparr/Reaparr/commit/3f1c5e5c7ae53b6a466c1f6776701dd56b8fdaa4))


### Reverts

* Revert "build(WebAPI): Revert last dev EF migrations" ([8f41e18](https://github.com/Reaparr/Reaparr/commit/8f41e181af2331a367d3d1429f68aaf887b71d7d))

# [0.35.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.34.0...v0.35.0-dev.1) (2025-12-26)


### Bug Fixes

* **WebAPI:** Added a temporary fix for validating an account token, i… ([#500](https://github.com/Reaparr/Reaparr/issues/500)) ([c22efe1](https://github.com/Reaparr/Reaparr/commit/c22efe1dda7aff2813140a9ba235d4133f4cb973))
* **WebAPI:** Added a temporary fix for validating an account token, it requires a larger Plex API SDK update ([afb6778](https://github.com/Reaparr/Reaparr/commit/afb6778f2a86570871a429be87ac201c39299843))
* **deps:** downgrade [@vueuse](https://github.com/vueuse) packages to version 13.x ([9e0c95a](https://github.com/Reaparr/Reaparr/commit/9e0c95a9227a9c5471cc6c3adf9861d27ca1dd97))
* **Web-UI:** Fix flashy UI changes when refreshing a library ([3c5ea7b](https://github.com/Reaparr/Reaparr/commit/3c5ea7bbe25682ef03b34b0384806d89ecae6d1a))
* **WebAPI:** Fix threading issues with the dbcontext in handlers ([372b922](https://github.com/Reaparr/Reaparr/commit/372b92241c3d52419531ad81fdd6c4ce411a8478))
* **WebAPI:** Fixed a bug where an auth token would validate but return a 400 bad request ([0b24793](https://github.com/Reaparr/Reaparr/commit/0b247932c3fc961cadc2960afedfddab9a77b976))
* **WebAPI:** Fixed an exception where during the progres update of a file transfer it would crash due to multiple threads using a DbContext ([e54a27f](https://github.com/Reaparr/Reaparr/commit/e54a27f78ad2e0b20e23e022cf62a523a6356054))
* **Web-UI:** Fixed an issue where some alerts had missing colors ([19b9850](https://github.com/Reaparr/Reaparr/commit/19b9850c5c70cef6ac03e4765d7243b7ebace411))
* **WebAPI:** Fixed an issue where the library sync progress would go above 100% ([df213b8](https://github.com/Reaparr/Reaparr/commit/df213b8302e960c8934eb688d9db877f55e268b4))
* **Web-UI:** Fixed an issue where the poster image title would go out of bounds when to long ([1c99c16](https://github.com/Reaparr/Reaparr/commit/1c99c16282f799012f77b6040e41ba410f19c8b7))
* **Web-UI:** Fixed an issue where the poster image would not show the fallback when loading fails ([e45393d](https://github.com/Reaparr/Reaparr/commit/e45393ddd57de0e20643de6b7d6a416f42fdc64b))
* **Web-UI:** Fixed an issue where the poster image would repeat after scrolling far enough on the wrong titles ([5170a9e](https://github.com/Reaparr/Reaparr/commit/5170a9eb3bf8b0c0fef04431cea9588d25f44abc))
* **Web-UI:** Fixed an issue where you could not save a Plex account by using a token ([af1aa56](https://github.com/Reaparr/Reaparr/commit/af1aa563ec169740a183700c4cde926a651c70b8))
* **WebAPI:** Fixed crash issues due to incorrect multi-threaded use of the dbcontext ([82727ab](https://github.com/Reaparr/Reaparr/commit/82727ab7a5656488b05e66cf7b7bbde1e1b17798))
* **Web-UI:** Fixed the confirmation button on the delete account dialog not showing the loading icon when it's busy ([ec9ed9f](https://github.com/Reaparr/Reaparr/commit/ec9ed9f3e94cb8063db80d906cde88f6fc76a37f))
* **WebAPI:** Increase HTTP client timeout for thumbnail image requests ([e70bfe3](https://github.com/Reaparr/Reaparr/commit/e70bfe3e2703938a630bcc5cf4a5d6504d5eb33b))
* **Web-UI:** Open on the correct auth tab and disable switching in edit mode in the Account Dialog ([81950ab](https://github.com/Reaparr/Reaparr/commit/81950abb2e86a324377e568bb3155869b0a4b2fc))
* **WebAPI:** resolve memory leak in download client disposal ([a99cdae](https://github.com/Reaparr/Reaparr/commit/a99cdae13f5669e4b7dbc31a44199c119cfe87cf))
* **WebAPI:** resolve SQLite ConnectionError[20004] during concurrent database access ([eb916e2](https://github.com/Reaparr/Reaparr/commit/eb916e220b01cb48700227a4171ae9a6c98fb673))
* **WebAPI:** use split queries to prevent cartesian explosion in collection includes ([d859772](https://github.com/Reaparr/Reaparr/commit/d8597721ff1c2bbb7286979654262ccaa6a2f0de))


### Features

* **WebAPI:** add dev prerelease support with semantic-release ([bb9b9ed](https://github.com/Reaparr/Reaparr/commit/bb9b9edc93a0a1786eb9020a7dbea4134a322a81))
* **Web-UI:** Add Radarr integration setup and connection features ([014ef2d](https://github.com/Reaparr/Reaparr/commit/014ef2d98e752696b24262787b2ef8262afad94d))
* **WebAPI:** Added apiKey authentication to the torznab indexer ([2ee560e](https://github.com/Reaparr/Reaparr/commit/2ee560e7d70b53ae25fcdcf6b403d8a1e7d458aa))
* **WebAPI:** Added changelog discord notification ([#508](https://github.com/Reaparr/Reaparr/issues/508)) ([1e0c69d](https://github.com/Reaparr/Reaparr/commit/1e0c69d61e1d856f2be2d2dfe31b722e91b6c723))
* **WebAPI:** Added changelog discord notification ([a73a0db](https://github.com/Reaparr/Reaparr/commit/a73a0db67db531af16c05f5adb02c4da34035f52))
* **WebAPI:** Added session based authentication to the Downloadclient that mimics QBittorrent ([904dfd1](https://github.com/Reaparr/Reaparr/commit/904dfd1ea92b57401f2f694a15051df1d20a06cc))
* **Web-UI:** Allow markdown to be rendered in the help dialog boxes to improve formatting and adding links ([b98b946](https://github.com/Reaparr/Reaparr/commit/b98b9464f607390e9dbfcad08a710423fe923582))
* **WebAPI:** Implement persistent auth session management for DownloadClient ([fba5e53](https://github.com/Reaparr/Reaparr/commit/fba5e5399637bc04984ddb0db8237bd842f3b5ae))
* **Web-UI:** Links in help text now open a new tab ([7309d2b](https://github.com/Reaparr/Reaparr/commit/7309d2bafff6bae4ac2d918e6f07cc2d7b375ff9))
* **Web-UI:** QOL: When executing Resync Server command, it will no close the dialog and open the correct sync dialog ([abd6703](https://github.com/Reaparr/Reaparr/commit/abd6703e378fb316c619a18e9e82f4f97f41e0dc))
* **Web-UI:** Removed the thick red bar around the media overview bar to a sleek small gray line to be consistent with the rest of the look ([5221d26](https://github.com/Reaparr/Reaparr/commit/5221d26cc028fa225d3974cba577f580ce705e2f))
* **Web-UI:** Sonarr can now be fully automatically configured from the integration page! ([e0da625](https://github.com/Reaparr/Reaparr/commit/e0da6252fcd4f3285ffb83311235210de1d1eb59))


### Performance Improvements

* **Environment:** add IsDevelopmentEnvironment helper and enable sensitive data logging in dev/test ([03afac0](https://github.com/Reaparr/Reaparr/commit/03afac0c201a6276f5b7b0e59eee47589a35a472))
* **Web-UI:** Added an optimization of using the GPU when scrolling fast across media posters ([e62b641](https://github.com/Reaparr/Reaparr/commit/e62b641f0ebc4b91ddfd3b9c8029e917d0ffa0a4))
* **WebAPI:** Hugely improved the performance of loading thumbnail for media posters, this will make navigating much smoother ([3e86fdd](https://github.com/Reaparr/Reaparr/commit/3e86fdd602e03f423ee41073f2b0f7657e38efc4))


### Reverts

* Revert "refactor(WebAPI): Add LibrarySyncJobQueue migration and update model snapshot" ([e4770d6](https://github.com/Reaparr/Reaparr/commit/e4770d6c4dbae582be9fc8ab62ee312fe096055f))

# [0.34.0](https://github.com/Reaparr/Reaparr/compare/v0.33.2...v0.34.0) (2025-10-19)


### Bug Fixes

* **WebAPI:** Added attribute to hide catch all endpoint from swagger document generation ([cea89df](https://github.com/Reaparr/Reaparr/commit/cea89df9ceb0d8049d82be4ebfc4c4ff7e9364c4))
* **WebAPI:** Allow Reaparr to automatically overwrite destination file after downloading and ending up with the same name ([3ea8325](https://github.com/Reaparr/Reaparr/commit/3ea832591bd271fe55f1f84a369447b71019cd94))
* **Web-UI:** Fix an issue where navigating to Plex libraries would duplicate the entry in the side menu ([1a595e0](https://github.com/Reaparr/Reaparr/commit/1a595e046f1a6840e5dda8175dfcae511c0a064c))
* **WebAPI:** Fixed an issue where restarting a download task will throw an error because it could not find the filepath ([8d32e3b](https://github.com/Reaparr/Reaparr/commit/8d32e3bb73e011ee41057378f4d7178ca42b549b))
* **Web-UI:** Fixed the issue with not being able to use username and password ([94c9fe6](https://github.com/Reaparr/Reaparr/commit/94c9fe6e84052a280593302945b3796f54e8b3dd))
* **Web-UI:** Prevent requesting image when needed data is missing ([1e9e2e3](https://github.com/Reaparr/Reaparr/commit/1e9e2e30a9b6bf1b17eae4af8afafb5e1bf37908))


### Features

* **Web-UI:** Add autofill attributes to Plex 2FA verification code to allow autocomplete from password managers ([cccd346](https://github.com/Reaparr/Reaparr/commit/cccd346c41c6c225ef9704eaeb8b2afa1b663f4b))
* **Web-UI:** Added a nice loading fade background to the poster images ([7993472](https://github.com/Reaparr/Reaparr/commit/799347262d6cca0945f56d8c2836540b203ac493))
* **Web-UI:** Added a toggle under Settings => Advanced => Download Manager to disable moving completed downloads out of the Downloads folder ([39e45f3](https://github.com/Reaparr/Reaparr/commit/39e45f3800db5802a2bef41dc41180ff13f2beaf))
* **Web-UI:** Added helpers to the Plex account input fields to allow password managers to fill in the username and password ([f0408d9](https://github.com/Reaparr/Reaparr/commit/f0408d9904cf0083f21c80331f1419503f9ae922))
* **WebAPI:** Reaparr will now download into a single file even though its still multi-threaded downloading. This skips the step of merging seperate parts making downloads faster ([f137f1a](https://github.com/Reaparr/Reaparr/commit/f137f1a449ca4d4bde5900bc8bd1f77f18b1146e))
* **WebAPI:** Support for trusted-proxy header authentication, allowing Reaparr to authenticate users based on a secure header (e.g. X-Auth-User) forwarded by a trusted reverse proxy instead of direct password login. ([36fc086](https://github.com/Reaparr/Reaparr/commit/36fc086bb861cffb002e95292ec7ed245dfb41c1))


### Performance Improvements

* **WebAPI:** Add response cache of an hour to the Plex media thumbnail ([e333f2a](https://github.com/Reaparr/Reaparr/commit/e333f2a7d44fcf81817c71cf8afd483079b9baf8))
* **Web-UI:** Cache Plex media images in the front-end and lower the requested quality ([c3bfe67](https://github.com/Reaparr/Reaparr/commit/c3bfe6791f231d94c6251872500f9b217e54c1bb))
* **WebAPI:** The data send to the front-end to show download progresss is now halved and should make it less likely for out-of-memory issues ([7e78595](https://github.com/Reaparr/Reaparr/commit/7e78595180a450f4158f4a39cd0a0bc06e3de5e6))


### Reverts

* **WebAPI:** Revert adding response caching from FastEndpoints, not needed for 1 endpoint ([8c5c8f2](https://github.com/Reaparr/Reaparr/commit/8c5c8f2feb0ba388c3fd4c8057e18d5b11136020))

## [0.33.2](https://github.com/Reaparr/Reaparr/compare/v0.33.1...v0.33.2) (2025-09-21)


### Bug Fixes

* **WebAPI:** Fixed permission issue on Unraid where it didnt take the PUID and PGID into account ([64332d0](https://github.com/Reaparr/Reaparr/commit/64332d03fd01cfb39edab4dcab18c6c9d81db4f7))
* **WebAPI:** Fixed permission issue on Unraid where it didnt take the… ([#495](https://github.com/Reaparr/Reaparr/issues/495)) ([1d69a47](https://github.com/Reaparr/Reaparr/commit/1d69a47e71ec85df6f28b6ede97d7053ada70573))

## [0.33.1](https://github.com/Reaparr/Reaparr/compare/v0.33.0...v0.33.1) (2025-09-18)


### Bug Fixes

* **WebAPI:** Fixed missing version from the Docker logs ([f8d8d77](https://github.com/Reaparr/Reaparr/commit/f8d8d770d0fc3e41b94c526e9e2288fadf8c9303))

# [0.33.0](https://github.com/Reaparr/Reaparr/compare/v0.32.0...v0.33.0) (2025-09-07)


### Bug Fixes

* **WebAPI:** Added more potential media qualities to parse ([c5888f8](https://github.com/Reaparr/Reaparr/commit/c5888f8deeea0c52346a850ab45876eeced6e747))
* **Web-UI:** Adjust dimensions of media filter menu for improved layout ([1590714](https://github.com/Reaparr/Reaparr/commit/15907146b4dc09e5ccccf0ce9694a407933243e8))
* **Web-UI:** Align media details in the MediaQTable for better readability ([a72aec4](https://github.com/Reaparr/Reaparr/commit/a72aec475fc803460251d4dd0f743ab0c3054ca4))
* **WebAPI:** Fixed a bug where plexLibrary metadata counts were not set correctly ([5c43b9c](https://github.com/Reaparr/Reaparr/commit/5c43b9ccf48738807f2551ce0585537631768919))
* **Web-UI:** Fixed an issue where if the filters were to strict and 0 results were availble, it would say the library would need to be synced ([fdfd5b6](https://github.com/Reaparr/Reaparr/commit/fdfd5b68ff041990bce40b06792f2a422d144bec))
* **Web-UI:** Fixed an issue where Music and Photo libraries were not displayed correctly and warned that they are not yet supported ([a935198](https://github.com/Reaparr/Reaparr/commit/a93519854f4feb065b7daf316f0adbc0220c4005))
* **WebAPI:** fixed missing qualities displaying next to episodes when viewing a TV show in detail ([e0b098e](https://github.com/Reaparr/Reaparr/commit/e0b098ea0fb89f715adf54599a9833cd4e365d77))
* **WebAPI:** Fixed the incorrect version being build into the docker image, leading to 0.0.0 ([336bc5d](https://github.com/Reaparr/Reaparr/commit/336bc5d541fa804329af50574bd9e69011fea28b))
* **WebAPI:** Fixed the issue where AddedAt and UpdatedAt were not parsed correctly, and thus it always displayed and 1970 date in the tv show detail screen ([743a93d](https://github.com/Reaparr/Reaparr/commit/743a93d08d572da44a446756ce7e6b8cb6108934))
* **WebAPI:** Fixed the qualities not being sorted when displayed in the UI ([044039a](https://github.com/Reaparr/Reaparr/commit/044039a93afd077a052b8bb22cc046aaaedd6813))
* **WebAPI:** Fixed the source generated info not being passed down when using LogByType ([a98e553](https://github.com/Reaparr/Reaparr/commit/a98e5537574133d8a3c1c97b8e96e6f72a26cf85))
* **Web-UI:** Fixed warning of missing translation import ([087e47d](https://github.com/Reaparr/Reaparr/commit/087e47d313a1e61589b983cf3863ec285c5bd113))
* **WebAPI:** Possible fix for first time Docker startup where the /Config folder is not owned by Reaparr ([7e13bb6](https://github.com/Reaparr/Reaparr/commit/7e13bb6c2599377c37c74ebc9e98ad0e5e9feda6))
* **WebAPI:** Possible fix for the first time boot of Reaparr crashing due to permission issues ([d8fb422](https://github.com/Reaparr/Reaparr/commit/d8fb422dced9809accb6f6d538a8ffb5354962c5))
* **Web-UI:** Prevent the flash of an warning while a library media overview is still loading ([2706166](https://github.com/Reaparr/Reaparr/commit/2706166fb01d6afaa51292cd94ba744c4f4f4f12))
* **WebAPI:** Retrieve PlexServerId and PlexLibraryId from episode data and add to the preview ([58eab89](https://github.com/Reaparr/Reaparr/commit/58eab8971f12c41137b45c97056627659c697f12))
* **Web-UI:** Selected quality for download is now working ([d192fdd](https://github.com/Reaparr/Reaparr/commit/d192fdddb918925e88d898d309737d3cbbe48e51))
* **WebAPI:** Sometimes Plex does not give any info on the media file, so now we parse the filename to at least determine quality ([175e736](https://github.com/Reaparr/Reaparr/commit/175e736a6d7d875a49e3f94b7af7ea92038ec7a5))
* **Web-UI:** The media filters are now cleared when moving to a different library ([f5863ba](https://github.com/Reaparr/Reaparr/commit/f5863ba4210c66676fb6922ee307f4a5010270af))
* **Web-UI:** Type errors from Vue-i18n upgrade ([27950f7](https://github.com/Reaparr/Reaparr/commit/27950f746d5aa206e7c7b5289b5fc7edc16a7c5d))
* **WebAPI:** Update avatar URL to use higher resolution image when posting a release in Discord ([9eed6ed](https://github.com/Reaparr/Reaparr/commit/9eed6edcfa597f02a9c69517134e9923e0e9ae92))
* **Web-UI:** Update branding and links to reflect Reaparr ([5064c69](https://github.com/Reaparr/Reaparr/commit/5064c69053af4d9d9d34b0aa0e0b85eb42224026))
* **WebAPI:** Update branding to not show links in console ([b089cf0](https://github.com/Reaparr/Reaparr/commit/b089cf0d7c2b3844874eb2cb38a67e1903451e4a))
* **WebAPI:** update logo path in README for correct display ([8a4a8d2](https://github.com/Reaparr/Reaparr/commit/8a4a8d2a9f8aa8dc261a2c8e42b9b2155c1e7dc2))


### Features

* **Web-UI:** Add a background to the glow chips such that they are more readable when hovering over them ([6e8300a](https://github.com/Reaparr/Reaparr/commit/6e8300ae1ee525ba6bb4cc7792e6fa96b5e8a66a))
* **WebAPI:** Added a migration to automatically convert the old named database and settings file to the new Reaparr one ([6c40df2](https://github.com/Reaparr/Reaparr/commit/6c40df2a92f31dca09efc68b6a4b696937c663a1))
* **Web-UI:** Added media quality overview to the Debug buttons page ([d578025](https://github.com/Reaparr/Reaparr/commit/d5780257b31480987f2b1d9405bbbd00cf7b2848))
* **WebAPI:** Added new Reaparr logo ([da89ecd](https://github.com/Reaparr/Reaparr/commit/da89ecdc4bad7cc7e543b511e6a748a43768b29c))
* **WebAPI:** Added svg logo for title ([#483](https://github.com/Reaparr/Reaparr/issues/483)) ([83ea99c](https://github.com/Reaparr/Reaparr/commit/83ea99cbbfe85ee25668dc9571fc6add4246e012))
* **WebAPI:** Added svg logo for title ([940ebd1](https://github.com/Reaparr/Reaparr/commit/940ebd121b31e09e287a76ca4a3c7d54bae0c395))
* **Web-UI:** Changed the navigation drawer icon to more accurately display what it does ([bf9f520](https://github.com/Reaparr/Reaparr/commit/bf9f520ffba479eca84fcb30be5e8a9ac5d56a45))
* **WebAPI:** Compressed ([d6983b2](https://github.com/Reaparr/Reaparr/commit/d6983b2dd9969c8eac4dabe5f3f6dcefcddfb25d))
* **Web-UI:** Enhance download confirmation dialog with media quality display ([49f62d3](https://github.com/Reaparr/Reaparr/commit/49f62d39c5f7611f0ed7fd365f238c2897083d15))
* **Web-UI:** Improved the coloring of media quality chips such that they are more instantly recognizable ([b16b367](https://github.com/Reaparr/Reaparr/commit/b16b367fe9df9f593f0b2ce92b84793a2a96e8e1))
* **Web-UI:** Made the version numbering in the Reaparr logo copy-able from the little V icon ([f295b52](https://github.com/Reaparr/Reaparr/commit/f295b523de1411e39cf79023e7e8a5186c17aa5f))
* **Web-UI:** Media can now be filtered on quality by clicking on the little magnify glass icon in the search bar ([051590a](https://github.com/Reaparr/Reaparr/commit/051590a82b064538f1f6b4088fd5e21055f2e3d0))
* **Web-UI:** TvShows now display all media qualities that are found within the episodes ([a0e72da](https://github.com/Reaparr/Reaparr/commit/a0e72daaa27320ef6737d11bd7a531309d6e8d58))


### Performance Improvements

* **Web-UI:** Moved some rendering logic to display the download preview to the back-end ([a8d71c8](https://github.com/Reaparr/Reaparr/commit/a8d71c8bca5565f0e02e2e00b40c19a19d614270))


### Reverts

* **Web-UI:** Revert Cypress upgrade for now ([d7e0cbe](https://github.com/Reaparr/Reaparr/commit/d7e0cbefc888aebcdad16ed9b190a340a5d9593b))
* **WebAPI:** revert migrations to later combine ([6f34cf2](https://github.com/Reaparr/Reaparr/commit/6f34cf2f3c90c90ee6cfc81ec2da02a9fb43d98e))
* **WebAPI:** revert migration ([ce30c94](https://github.com/Reaparr/Reaparr/commit/ce30c94bbecef163f962786ccfc9712495bc0bdd))
* **WebAPI:** set dotnet install back ([d9d7223](https://github.com/Reaparr/Reaparr/commit/d9d7223a26fa7012ca84cbc443543323f247dcb5))

# [0.32.0](https://github.com/Reaparr/Reaparr/compare/v0.31.1...v0.32.0) (2025-07-20)


### Bug Fixes

* **WebAPI:** fix library sync failing when there is a music library ([ba2def8](https://github.com/Reaparr/Reaparr/commit/ba2def8fc3b99780bbea12365e142c9ede3dee92))
* **WebAPI:** Fixed an issue where duplicate metadata relations could be inserted, throwing exceptions ([840486e](https://github.com/Reaparr/Reaparr/commit/840486e60faa6055eee3cb624cf5e8a7bd80a90a))
* **WebAPI:** Fixed an issue where episodes could not be downloaded ([0939a9f](https://github.com/Reaparr/Reaparr/commit/0939a9f806ba787586d414d2d494456f9f908a83))
* **WebAPI:** Fixed an issue where the database was not reset correctly when an attempted migration failed ([61c58c4](https://github.com/Reaparr/Reaparr/commit/61c58c447c2fe31dc68fce47cea3fec3d8caf7a2))
* **WebAPI:** Fixed an issue where the folder browser would throw exceptions when there were stale file handles ([08ba5f4](https://github.com/Reaparr/Reaparr/commit/08ba5f42e386e9c1f30dd871d5678b3d3a021de8))
* **WebAPI:** Fixed an issue where you could not login with username and password ([0d8f5b0](https://github.com/Reaparr/Reaparr/commit/0d8f5b0436c77d49c4b77f8019bfed2f72951ccb))
* **Web-UI:** Fixed and improved the media filter menu in the searchbar ([67c9713](https://github.com/Reaparr/Reaparr/commit/67c9713aeb03f63ed826936f8ac076c638fd15b4))
* **Web-UI:** Fixed incorrect count of active downloads ([23c2ba0](https://github.com/Reaparr/Reaparr/commit/23c2ba0c2ee31e604f9c8cca567c126063052168))
* **WebAPI:** fixed incorrect genre and country update when syncing a movie library ([5ac503b](https://github.com/Reaparr/Reaparr/commit/5ac503b1b330fb9b74e54e8c918ba333f93e99b7))
* **Web-UI:** Fixed media posters not loading when using firefox with "https-only-mode". Now Reaparr will prioritize https connections and thus prevent this from happening ([7d961bb](https://github.com/Reaparr/Reaparr/commit/7d961bbb7902b6394538141b739dbe159ea9f532))
* **Web-UI:** Fixed missing quality chips from media overview in the front-end ([4eafebb](https://github.com/Reaparr/Reaparr/commit/4eafebb0ca021a0c20b7ebb894f5a635864418de))
* **WebAPI:** Fixed missing quality chips from media overview ([8a81d4c](https://github.com/Reaparr/Reaparr/commit/8a81d4c1e3f7ff386e95d9d2f27a6469ce8a121a))
* **WebAPI:** Fixed the FirstTimeSetup property not reset to true when the database is reset ([1fdd752](https://github.com/Reaparr/Reaparr/commit/1fdd752b3575f8e9e1ff1152c52719d2732fb0fe))
* **WebAPI:** Rename test method to reflect filtering logic for actors with empty keys ([ae0f52b](https://github.com/Reaparr/Reaparr/commit/ae0f52b6fb3226a9db29c60ad2f2fd2c65344432))
* **WebAPI:** Use drive name instead of volume name for file system model when an exception is thrown ([098e84b](https://github.com/Reaparr/Reaparr/commit/098e84b1ac4375f35fc848b19abf11bd8d85dfdf))


### Features

* **Web-UI:** Added a metadata search input field so you can find a specific actor more easily ([5ca067e](https://github.com/Reaparr/Reaparr/commit/5ca067eebe12333dc90b0f2b59eb40fd68cc72e2))
* **Logging:** Added setup for FluentResults integration and result logging configuration ([38d69db](https://github.com/Reaparr/Reaparr/commit/38d69dbfc93f39e9da4e22258d2f5350379d07a9))
* **WebAPI:** allow all media details to be synced into Reaparr, such as subtitles, roles, countries etc ([9793869](https://github.com/Reaparr/Reaparr/commit/9793869d8f2b82b4becf795a0929aeecb95293b7))
* **WebAPI:** enhance media metadata sync with detailed logging and performance tracking ([abd7496](https://github.com/Reaparr/Reaparr/commit/abd749618d8a1d132279ed87f44e1bd29ba7e039))
* **WebAPI:** implement HTTPS prioritization and add ChosenConnection flag when sending all connections ([3cf41cf](https://github.com/Reaparr/Reaparr/commit/3cf41cfc753d77fd3846fe9facbcb337359b4fde))
* **WebAPI:** Make the logging clickable and open the place where something has happened ([a8213ac](https://github.com/Reaparr/Reaparr/commit/a8213ac05af9d42b302d1b445f05b3b5ce4ec7cd))
* **Web-UI:** Make the metadata header in the media overview menu stick to the top to always be visible ([c330bc2](https://github.com/Reaparr/Reaparr/commit/c330bc2b8ba169fd263198edb8393e66c75624c5))
* **Web-UI:** When refreshing a page, instead of displaying 0%, show an indeterminate loading icon ([b6b58a2](https://github.com/Reaparr/Reaparr/commit/b6b58a2c175010b0e7e437863ead4963c0e4ffc5))


### Performance Improvements

* **WebAPI:** greatly improve performance of requesting and querying library metadata such as genre, countries and actors ([f2a1db8](https://github.com/Reaparr/Reaparr/commit/f2a1db80ecce5a00ab8bbf4be04a36b8e1d6b9e0))
* **WebAPI:** greatly improved the performance of loading a media library, this should resolve the 10 min wait with big libraries ([71b1f77](https://github.com/Reaparr/Reaparr/commit/71b1f77883f84dc042f3539a71d43f61e4fc1754))
* **WebAPI:** greatly improved the performance of loading the home page library ([888d3c2](https://github.com/Reaparr/Reaparr/commit/888d3c24c2e4b027bf42595aeea8d95005547c98))
* **Web-UI:** Improve media overview performance by loading the media and metadata at the same time ([ee8a2a3](https://github.com/Reaparr/Reaparr/commit/ee8a2a3a6e132285f7357dffd8e47d5260c8d536))
* **Web-UI:** Made the loading of library metadata not block the media view, just make it load in the background ([319ba83](https://github.com/Reaparr/Reaparr/commit/319ba83443ba889ca6ca6e422e5d7170b171362e))
* **WebAPI:** Minor performance improvement how strings are converted from and to enums ([bae1bf1](https://github.com/Reaparr/Reaparr/commit/bae1bf1e9a32593df19a00e433f3b4dbcbc0bb94))
* **WebAPI:** optimize MD5 hash computation and hex conversion ([d0d5821](https://github.com/Reaparr/Reaparr/commit/d0d58214da5253a9356f57bd0b10d53e60214f29))
* **WebAPI:** optimize media title and genre generation using lazy loading ([336520c](https://github.com/Reaparr/Reaparr/commit/336520cf2789f225d9ad27099c459acdee0413a9))
* **WebAPI:** optimize metadata sync with bulk operations ([ae87a03](https://github.com/Reaparr/Reaparr/commit/ae87a03686025339f94aea169a69500348d524cb))
* **WebAPI:** wrap bulk operations in transactions to ensure consistency ([2739e2e](https://github.com/Reaparr/Reaparr/commit/2739e2e4451913d9c8c57341efbd00813b0ab6b2))


### Reverts

* **WebAPI:** revert snapshot ([4353ecd](https://github.com/Reaparr/Reaparr/commit/4353ecdaea099601b61425c346d13bee9859f9f1))

## [0.31.1](https://github.com/Reaparr/Reaparr/compare/v0.31.0...v0.31.1) (2025-04-27)


### Bug Fixes

* **WebAPI:** Fixed an issue where multiple devices would show up when logging in with Reaparr ([1057d91](https://github.com/Reaparr/Reaparr/commit/1057d91721c8d5b1146d8a3dd05fcbd8c286d6f9))
* **WebAPI:** Fixed an issue where TvShow libraries were not recognized and instead shown as unknown ([06f4d6f](https://github.com/Reaparr/Reaparr/commit/06f4d6f460f5765dd1b3d089cf30d297a7951d64))
* **Web-UI:** Fixed padding around error alert when the library type is unknown ([b9d66e3](https://github.com/Reaparr/Reaparr/commit/b9d66e34932ba20f34ef8d7a180c178c69f22723))

# [0.31.0](https://github.com/Reaparr/Reaparr/compare/v0.30.0...v0.31.0) (2025-04-13)


### Bug Fixes

* **WebAPI:** Fixed an issue where the user could not login with username and password ([f3289c8](https://github.com/Reaparr/Reaparr/commit/f3289c8314144e9e978b907cf15319cb2260a0c5))
* **WebAPI:** Fixed an issue where the user would be logged out of Reaparr when an PlexAccount would have lost access or become unauthorized ([11cb4a5](https://github.com/Reaparr/Reaparr/commit/11cb4a5079a4b6983005618f1cf84c63ba313c8f))
* **WebAPI:** fixed an migration issue due to changing the metadata object change ([cf07a65](https://github.com/Reaparr/Reaparr/commit/cf07a65d4a85ec1283eb9560b993854e51e3ae5e))
* **Web-UI:** fixed Cypress Api port not being set correctly ([35bfc5c](https://github.com/Reaparr/Reaparr/commit/35bfc5c56e805ce3a0e725b3a874da6a03c20c74))
* **Web-UI:** Fixed the Confirm Password for changing the Reaparr credentials to show "Password Required" error after successfully saving ([0b4494d](https://github.com/Reaparr/Reaparr/commit/0b4494dd944fb3fa835b2cfdc33cd8e51434df8b))
* **Web-UI:** Fixed the switching between username/password and token mode. The values were sometimes filled in when validating and not cleared correctly when switching ([17c06ed](https://github.com/Reaparr/Reaparr/commit/17c06ed18d4bc9c5e5eb3c3956c0fd7be7a6cb8a))
* **WebAPI:** Fixed the unmasked env variable not working when running in Docker ([02a8f61](https://github.com/Reaparr/Reaparr/commit/02a8f618503321c1645651760fb92f08fb0460e8))
* **WebAPI:** Separate the custom auth token from the auto filled in from the Plex API ([8bcdc39](https://github.com/Reaparr/Reaparr/commit/8bcdc3994bea8d8c90beb0be84d48aca429075af))


### Features

* **WebAPI:** Added a new env variable LOG_ENV_VARS to dump the env values on Reaparr start-up ([7a1bc26](https://github.com/Reaparr/Reaparr/commit/7a1bc26bd50bf117ea79b7ca2b107dbafe216ec8))
* **WebAPI:** Added endpoints to send Role, Genre and Country library metadata to the front-end ([78610ce](https://github.com/Reaparr/Reaparr/commit/78610ce5b26a762f5b566eedf357a76d5d642d01))
* **Web-UI:** Media can now be filtered by Country, Role and Genre by clicking on the magnify glass icon in the searchbar ([fdb3cfe](https://github.com/Reaparr/Reaparr/commit/fdb3cfe25b119dd9dbb1c596b16ce343af91f597))


### Performance Improvements

* **WebAPI:** Greatly improved performance for generating mock data for the HttpClient handler ([46503ef](https://github.com/Reaparr/Reaparr/commit/46503ef1dcc2e190fec7b6973adf94fe204dabe3))
* **WebAPI:** smaller improved performance for generating mock data for the HttpClient handler ([87c9ec8](https://github.com/Reaparr/Reaparr/commit/87c9ec88d3e63f4cd2fc781b6fe272f8469fad9b))

# [0.30.0](https://github.com/Reaparr/Reaparr/compare/v0.29.1...v0.30.0) (2025-02-01)


### Bug Fixes

* **WebAPI:** Fixed an issue where user data on PlexLibraries was overwritten when refreshing Plex library access ([21c00fc](https://github.com/Reaparr/Reaparr/commit/21c00fc5722ba9b788e0765482e30baa6059e91c))
* **Web-UI:** fixed background jobs active in the front-end not resumed when refreshing the page ([3b8a113](https://github.com/Reaparr/Reaparr/commit/3b8a113afd4307a6bab712aab09cdfbccbf2781c))
* **Web-UI:** fixed cypress boot process by using es-module import ([4bf512e](https://github.com/Reaparr/Reaparr/commit/4bf512e34257bcef8508a532ad6aafc965f0c65c))
* **WebAPI:** Fixed incorrect available space calculation when using a root path "/" ([5ffe479](https://github.com/Reaparr/Reaparr/commit/5ffe47945c02222b7c463d75e0a83e7b6209eff7))
* **WebAPI:** Fixed missing restart button on downloadtasks that are in an move or merge error state ([4b40878](https://github.com/Reaparr/Reaparr/commit/4b408781774faedcaf07dec9b059146152d82b01))
* **WebAPI:** Fixed multiple download tasks merging/moving files, in most cases this will just make it slower for all so now this is limited to 1 ([6ba3cfb](https://github.com/Reaparr/Reaparr/commit/6ba3cfbb7d7641c790cd970ad8db401f2c897a80))
* **WebAPI:** fixed percentage miscalculation when Reaparr is syncing the media ([af5da57](https://github.com/Reaparr/Reaparr/commit/af5da5713726985bfa0e0035b63bf240bcefa373))
* **WebAPI:** Fixed the default destinations not being shown in the PlexServer settings dialog ([cb55c0b](https://github.com/Reaparr/Reaparr/commit/cb55c0b6fd8f29285ea4da309b14662da410ebc4))
* **Web-UI:** Fixed the sync media server background job not updating correctly in the front-end ([37f22f1](https://github.com/Reaparr/Reaparr/commit/37f22f1241241c4bda043c55b5cb8f67bf154af8))


### Features

* **Web-UI:** A plex access report is now shown when refreshing a Plex account access in the account menu ([dbf9d80](https://github.com/Reaparr/Reaparr/commit/dbf9d80712568b075683b21ae8600d7a0d67907f))
* **WebAPI:** Add property whether a server is offline and therefore could not determine the plex libraries ([e92fb3a](https://github.com/Reaparr/Reaparr/commit/e92fb3a62ef19da77cf48dd11430eadb676b784e))
* **Web-UI:** Added a click option under server when there are no libraries to refresh access more easiliy for new users ([8734864](https://github.com/Reaparr/Reaparr/commit/873486460e247447aaf4e520f050eb61916f6ed1))
* **Web-UI:** Added a Plex Account Access Report which will show when refreshing the PlexAccount access: Account menu in top bar => refresh account ([2d76444](https://github.com/Reaparr/Reaparr/commit/2d7644485f0a29545c86d98e1a35e941b52c87d5))
* **WebAPI:** Added endpoint for server and library access refresh ([e176bf1](https://github.com/Reaparr/Reaparr/commit/e176bf1a35e60d7fa0598066d393f7f0fffa3adf))
* **Web-UI:** Display when a server connection is through Plex as a relay in the server settings ([6293e8e](https://github.com/Reaparr/Reaparr/commit/6293e8e5fb5d1fa9f0c9dc01fde5e3bd4d357ab0))
* **Web-UI:** Hovering over a tv show poster will show how many seasons and episodes there are ([3c6c5e1](https://github.com/Reaparr/Reaparr/commit/3c6c5e1fe43a86f588673a88671deec7a85c4b5f))
* **Web-UI:** Improved the look of all badges, such as the little number on top of the notification button and background activity toggle ([fe3454a](https://github.com/Reaparr/Reaparr/commit/fe3454a664b5bd5cab1eb444e494563e6e927f30))
* **Web-UI:** Improved the look of all chips, such as the quality circles on movie posters by making them glow ([6e63aeb](https://github.com/Reaparr/Reaparr/commit/6e63aebf6a88ae62683343deb9b1eabbf34915fa))
* **WebAPI:** Reaparr Docker images will now contain meta information based on the Opencontainers.image-spec ([b5ac1aa](https://github.com/Reaparr/Reaparr/commit/b5ac1aaaa5b8c2a1bd8fdb861b68f5daffcde0f7))
* **Web-UI:** Pressing the Download action will now show a loading icon and disable other actions to make it dummy proof and prevent multiple commands ([9f2323e](https://github.com/Reaparr/Reaparr/commit/9f2323eb5657fec74b34afdae9af2f30638e94ba))


### Performance Improvements

* **WebAPI:** Do not send the download actions with every download update, this will be determined by the front-end ([35c70c4](https://github.com/Reaparr/Reaparr/commit/35c70c4f9fceacb4959356c0709c0a0652f18acc))
* **WebAPI:** Removed unused properties from the ResultDTO response class ([a3df362](https://github.com/Reaparr/Reaparr/commit/a3df36247e1131267c72f1e96831d9d3bea0a40d))

## [0.29.1](https://github.com/Reaparr/Reaparr/compare/v0.29.0...v0.29.1) (2025-01-19)


### Bug Fixes

* **WebAPI:** Fixed an issue where two downloads on the same server could happen, now an extra check is added to prevent this ([1b33455](https://github.com/Reaparr/Reaparr/commit/1b33455d634ca98d156d454d95fe3e83e93ce81b))
* **Web-UI:** Fixed the duration display with the correct zero padding ([5d7b258](https://github.com/Reaparr/Reaparr/commit/5d7b25891c2dd6f3011cc7b1b186421fa48c7e39))
* **WebAPI:** Fixed the issue again where download task selection would stall due to incorrect status determination ([f300a0f](https://github.com/Reaparr/Reaparr/commit/f300a0f4fded8c7b5e56db4a13a4016a729be7c2))
* **WebAPI:** possible fixed the download exceptions that can happen due to bad connections and now Reaparr retrying that ([961b65e](https://github.com/Reaparr/Reaparr/commit/961b65e50ecf8a741a1e1aa53770f0899e528380))


### Performance Improvements

* **WebAPI:** micro improvement by removing autofac dependency assembly scanning in Application project ([0e708a6](https://github.com/Reaparr/Reaparr/commit/0e708a68e4c27c928a58c7a114181cee26078242))

# [0.29.0](https://github.com/Reaparr/Reaparr/compare/v0.28.0...v0.29.0) (2025-01-18)


### Bug Fixes

* **WebAPI:** abort boot sequence when config or database cannot be set ([adc7337](https://github.com/Reaparr/Reaparr/commit/adc73371d9fc388a4b3e423fd9955cc6cbefe2ac))
* **Web-UI:** added a page load overlay when logging in ([37ac772](https://github.com/Reaparr/Reaparr/commit/37ac772af6cab5bbb3d58b6d3e6fd9cce32f8c2f))
* **Web-UI:** agreeing to the disclaimer is now stored in the settings file ([99e8a43](https://github.com/Reaparr/Reaparr/commit/99e8a43588acafd849af6ebe76afa33b32438373))
* **Web-UI:** allow authentication status endpoint to be viewed anon ([6509b9c](https://github.com/Reaparr/Reaparr/commit/6509b9ca99b30cbd0aad9ec6036460ebc5d845dc))
* **Web-UI:** always hide horizontal scroll bar, 99% is always a few pixels ([355b681](https://github.com/Reaparr/Reaparr/commit/355b68199853fa3bf41430edb40a1b6cda3f03a3))
* **WebAPI:** authentication now is valid for 6 hours ([c593e11](https://github.com/Reaparr/Reaparr/commit/c593e1197134253df61408087cffb58d33dd6756))
* **Web-UI:** convert API exceptions due to error codes to ResultDTO ([59d90f7](https://github.com/Reaparr/Reaparr/commit/59d90f76fbc5660f45e37699749b336a5292f2e0))
* **Web-UI:** Finishing the setup process will not require a full page reload anymore, making the transition much smoother ([ae6874b](https://github.com/Reaparr/Reaparr/commit/ae6874b34814848031bd97bfae67b209c4a1c483))
* **WebAPI:** fix a dev release being made when only the readme has changed ([ac14418](https://github.com/Reaparr/Reaparr/commit/ac14418ef3bd8fe91e6d67d5210352fa9a1a7322))
* **Web-UI:** fix blank screen when setting up a new database ([3f53c43](https://github.com/Reaparr/Reaparr/commit/3f53c43e0b9c25d102efd11ae67262d3ce2e08cf))
* **WebAPI:** fix misconfigured authentication, everything working perfect now ([e993733](https://github.com/Reaparr/Reaparr/commit/e9937332d763a88bc1c36c5f6bea61637ac61a9e))
* **Web-UI:** fix the alphabet letters not working and displaying when scrolling through the media ([c27f111](https://github.com/Reaparr/Reaparr/commit/c27f11131ed2b67703a8620a757e6621825b8ace))
* **Web-UI:** fix the cypress configuration to run in CI/CD ([9aa4283](https://github.com/Reaparr/Reaparr/commit/9aa428325e032f0275188de8c9af7e19bcce34b2))
* **Web-UI:** fix the extra scrollbar that appears on the media page ([b92f06b](https://github.com/Reaparr/Reaparr/commit/b92f06b1153f843b2bb8e090bed804e59d90d4b0))
* **Web-UI:** Fixed a bug where a PlexToken needed a special character to register ([51ea3ef](https://github.com/Reaparr/Reaparr/commit/51ea3ef1a4ab6d2cca6c8fc5d52d090bdf9efc7a))
* **WebAPI:** Fixed a download status to download task phase conversion issue that could cause errors when picking the next download ([acb5b88](https://github.com/Reaparr/Reaparr/commit/acb5b885ba218bca1580993e4a5b252572ccaab8))
* **WebAPI:** Fixed an aggregate download status conversion issue that could cause errors when picking the next download and stalling ([2510fb5](https://github.com/Reaparr/Reaparr/commit/2510fb57728a6fef0468658cf4127b8e4ded293e))
* **Web-UI:** fixed an issue were some stores were not reset when logging out ([916dbad](https://github.com/Reaparr/Reaparr/commit/916dbadcb1190b10c813adca01acf07acd5f617c))
* **WebAPI:** Fixed an issue where not all episodes were linked to a season due to missing data, this is now resolved by using a different parameter to match ([38148ff](https://github.com/Reaparr/Reaparr/commit/38148ff2ff0a2e624408539d7c0e3846e6b38a2d))
* **WebAPI:** fixed an issue where the PlexAPI returning and 401 will log the user out of Reaparr ([ae6befc](https://github.com/Reaparr/Reaparr/commit/ae6befc7209d51800314dae1d65bfc1a91371ba4))
* **Web-UI:** fixed javascript exception when there is no media available in the media overview ([ad6c7ad](https://github.com/Reaparr/Reaparr/commit/ad6c7ad0bfdbd7504e27e22c9bd7a9560bd03768))
* **Web-UI:** fixed missing translations for the account input fields validation errors ([ddac5cb](https://github.com/Reaparr/Reaparr/commit/ddac5cbf08e97a02b3214658ad2348b49c669111))
* **Web-UI:** Fixed missing translations for the download-confirmation destination options ([b9ff5c2](https://github.com/Reaparr/Reaparr/commit/b9ff5c2b6a5e374c718deb9ebcefb9d013ee014b))
* **Web-UI:** fixed missing translations for the password input field validation errors ([b1178be](https://github.com/Reaparr/Reaparr/commit/b1178bebe1d4714fa406bd46864fa69c61725075))
* **Web-UI:** fixed missing translations from the background activity toggle ([a2d914d](https://github.com/Reaparr/Reaparr/commit/a2d914d7163440d1ea53ad3c83cffd4570f32a61))
* **Web-UI:** fixed the download confirmation destination path not triggering the download when selecting an episode ([9a0d5fb](https://github.com/Reaparr/Reaparr/commit/9a0d5fb30163c203070c4a074ea3ccfa2b3582a2))
* **Web-UI:** fixed the layout of the custom download destinations in the download confirmation window ([20f4dba](https://github.com/Reaparr/Reaparr/commit/20f4dba8ff0285da5fe8a8af52ee948ffb57afe7))
* **Web-UI:** fixed the missing title and ico ([006d10b](https://github.com/Reaparr/Reaparr/commit/006d10bf38077ed2d94a056f0e916794341845fa))
* **Web-UI:** fixed the outlining of the media table when viewing a media library ([ed11014](https://github.com/Reaparr/Reaparr/commit/ed110142e047bbb76acc18a937a3d390820ef6d8))
* **Web-UI:** fixed the setup and discord popups not appearing again when already on the setup page ([a0f8d37](https://github.com/Reaparr/Reaparr/commit/a0f8d37fbfb897cec3d7b70193605adef097060c))
* **Web-UI:** fixed the static background not stretching when viewing on 4k size ([9bd68d7](https://github.com/Reaparr/Reaparr/commit/9bd68d701d16f4ba4efab90b0c7be3aebf6c2e2c))
* **WebAPI:** Fixed the verbose log strings in the Download task logging, now its more clear ([67eb960](https://github.com/Reaparr/Reaparr/commit/67eb9607fe17c51cdc7a204c54f449c2c5402221))
* **WebAPI:** fixed the web ui not being visible when not logged in ([ca451e3](https://github.com/Reaparr/Reaparr/commit/ca451e3a9a56fd334332dc9d095f81e55b5fa36c))
* **Web-UI:** further cleanup of layout ([a376117](https://github.com/Reaparr/Reaparr/commit/a376117fa491d378073b4d587a1650daf6a50691))
* **Web-UI:** ignore .idea folder with eslint ([b538bac](https://github.com/Reaparr/Reaparr/commit/b538baca91a3fd60874c1287f32e714bd877d58d))
* **Web-UI:** prevent the settings store from failing when resetting and then using it ([612df2a](https://github.com/Reaparr/Reaparr/commit/612df2aef03ead5463e39feaf7ca441316ef60f1))
* **WebAPI:** removed duplicate call ConfigureApplicationCookie ([8ac26cf](https://github.com/Reaparr/Reaparr/commit/8ac26cf936095923cf597d87cdbaeaf640123aa4))
* **WebAPI:** stop container when failing to create config file ([451d94c](https://github.com/Reaparr/Reaparr/commit/451d94c5a0325fe69043577b26fb0eba4c58bec2))


### Features

* **Web-UI:** Add rememberMe option to the login screen and show when the user is timed-out due to failed attempts ([de979ae](https://github.com/Reaparr/Reaparr/commit/de979aeb3def9e6f7d4a2fc5d352d068faf8c537))
* **Web-UI:** Added a login page to finally secure Reaparr ([7ea5804](https://github.com/Reaparr/Reaparr/commit/7ea58048fcf5c7cc701ef67ad8cb196830457c84))
* **WebAPI:** added endpoints to update and get the Reaparr app credentials ([064557a](https://github.com/Reaparr/Reaparr/commit/064557aa521ba031c51d0cd8b5e9694f262f0120))
* **Web-UI:** added forgot password link to the login window ([9365296](https://github.com/Reaparr/Reaparr/commit/9365296179183477d12d692adeb622b71e983ded))
* **Web-UI:** added password strength to the Authentication section ([b70885b](https://github.com/Reaparr/Reaparr/commit/b70885b1e91ee70191a5b49e31b66baea8e8fe07))
* **WebAPI:** Added remember me to keep the being logged in when the browser is restarted ([12009aa](https://github.com/Reaparr/Reaparr/commit/12009aa1ce71f487ce07c7755828058999225894))
* **WebAPI:** dev releases not have their release notes generated, making it easier to see what is fixed  a31c5e ([6e639ff](https://github.com/Reaparr/Reaparr/commit/6e639ff1166cf00832711c17851fd550ad0b4b06))
* **WebAPI:** dev releases not have their release notes generated, making it easier to see what is fixed ([8199e4d](https://github.com/Reaparr/Reaparr/commit/8199e4de9b9c6ef442261be2f0bf4381f6c54f02))
* **Web-UI:** Greatly improved the layout of the settings and made it more screen size responsive, it's almost the same as to how Sonarr/Radarr does it, and it looks much better now ([8c7d03f](https://github.com/Reaparr/Reaparr/commit/8c7d03f3160c18f6e2ea7f01fed1a111efffd290))
* **Web-UI:** Removed the option to skip the setup process, now Reaparr will automatically redirect to the setup page ([132ab0a](https://github.com/Reaparr/Reaparr/commit/132ab0a5a4b040e895cfe500bc9ecd2afe83ed38))
* **Web-UI:** removed the slider from the download speed limit as it was just annoying to use ([48a91e8](https://github.com/Reaparr/Reaparr/commit/48a91e8a3cf696d93cb57444555501cf5da38b76))
* **Web-UI:** revamped the setup process by adding a disclaimer and a way to change the default login credentials ([54eb361](https://github.com/Reaparr/Reaparr/commit/54eb361b962c781dcbc065ca9d6da8b4b8411114))
* **WebAPI:** The Reaparr app user credentials can now be reset by setting the ResetCredentials to true in the settings file ([c4a2d6c](https://github.com/Reaparr/Reaparr/commit/c4a2d6c9fd96774568b0b1f9ab779651c1390a79))
* **Web-UI:** The Reaparr login username and password can now be changed ([9d47d45](https://github.com/Reaparr/Reaparr/commit/9d47d4594b4a2e485a0bddda515b27c4c7b0c79b))
* **Web-UI:** turned the account selector button in the appbar to the same style as the other buttons ([328156f](https://github.com/Reaparr/Reaparr/commit/328156fbfa9511a414bddccfe490313418ba33b8))


### Reverts

* **Web-UI:** Revert upgrading swagger-typescript-api and Typescript ([eef29de](https://github.com/Reaparr/Reaparr/commit/eef29de2b4edcfe28f5abf8830854430b0afe853))

# [0.28.0](https://github.com/Reaparr/Reaparr/compare/v0.27.0...v0.28.0) (2024-12-22)


### Features

* **Web-UI:** In the DownloadConfirmation window, you can now select the destination for the media you're about to download ([35d3411](https://github.com/Reaparr/Reaparr/commit/35d3411915433ea89aabbf6fd23186695393288e))

# [0.27.0](https://github.com/Reaparr/Reaparr/compare/v0.26.0...v0.27.0) (2024-12-19)


### Bug Fixes

* **Web-UI:** Added a bit of padding around elements in the HelpRows ([22f7977](https://github.com/Reaparr/Reaparr/commit/22f7977e64b24a11d6acdc817e1db26024fee5e3))
* **Web-UI:** Added a more reliable way of displaying the library metadata by going of the media api call instead of the library call ([76a4194](https://github.com/Reaparr/Reaparr/commit/76a41946771f46406fdda294e81b9f59224f4dee))
* **WebAPI:** Added missing new download status to the aggregation method ([4d5cccf](https://github.com/Reaparr/Reaparr/commit/4d5cccf06b3aa38b372f81eb30676c746da000d0))
* **WebAPI:** Allow more retries when downloading a file before erroring out ([3e67bc4](https://github.com/Reaparr/Reaparr/commit/3e67bc4ce0dd15259fbcdb33d755177cb8fe719b))
* **WebAPI:** Allow TaskCanceledException for FileMergeJob when pausing the job ([b5ac742](https://github.com/Reaparr/Reaparr/commit/b5ac742265dc4bb6ab511ed73b5e4106450f5831))
* **WebAPI:** Allow X-Reaparr-Version header in the CORS ([b69fc95](https://github.com/Reaparr/Reaparr/commit/b69fc95485c2e25d58dc1d350727914c3e2f2140))
* **WebAPI:** Calculate progress when manually updating the download task status during testing ([0cc8fca](https://github.com/Reaparr/Reaparr/commit/0cc8fcac825bd12d6278cfff303e1275c8d9284d))
* **Web-UI:** Clicking the discord button will now open the discord invite dialog and not the invite link directly ([56ae561](https://github.com/Reaparr/Reaparr/commit/56ae561a224dce2231a83ea9e538c5ab1d7a2d52))
* **WebAPI:** Display the dev version in the boot log correctly ([f275e1d](https://github.com/Reaparr/Reaparr/commit/f275e1dd7c209981863a30f3c927136a4e49706c))
* **WebAPI:** Fixed 0% percentage when downloadtask is completed ([9ba99de](https://github.com/Reaparr/Reaparr/commit/9ba99de686d576203401279fec38afa7979262e5))
* **WebAPI:** Fixed incorrect error message hiding the exception when a download is being retried ([c6d9840](https://github.com/Reaparr/Reaparr/commit/c6d9840aed87c931a78e50d495a695908e013ed9))
* **Web-UI:** Fixed incorrect server sorting in the server drawer, it's now all owned servers and then alphabetically ([ebbbbe2](https://github.com/Reaparr/Reaparr/commit/ebbbbe2a944fa0f990ed51630020e4e884f2f07c))
* **WebAPI:** Fixed merge/move progress miscalculating the transfer speed  and therefore the time remaining ([ade2109](https://github.com/Reaparr/Reaparr/commit/ade210969fe17d776c923492984516aa9841e967))
* **WebAPI:** Fixed missing mergePaused and movePaused status from DownloadTask parent tree status calculation ([41325e8](https://github.com/Reaparr/Reaparr/commit/41325e8d08d661b72474ba82275b555921ac7d79))
* **Web-UI:** Fixed Plex Accounts not refreshing when 1 is deleted ([f76b1c5](https://github.com/Reaparr/Reaparr/commit/f76b1c5184f17d8f747cda30919c1274e2ac46bb))
* **Web-UI:** Fixed the download button appearing without anything selected to download ([5e5dca1](https://github.com/Reaparr/Reaparr/commit/5e5dca1315b1999378f558966b400b57c48d4667))
* **WebAPI:** Fixed the DownloadWorkerTasks not being cleaned up after the download task is done ([c7c4044](https://github.com/Reaparr/Reaparr/commit/c7c4044ff06d1c7b583e10f142ed236a2e0230f5))
* **WebAPI:** Fixed the incorrect time remaining on the TvShow level, now it displays it based on the speed of the download and the remaining total data to be downloaded ([f1758f4](https://github.com/Reaparr/Reaparr/commit/f1758f485c9a4e917f29f60831a131f3d809ed07))
* **WebAPI:** Fixed tvshow folder being left behind after it has been moved/merged from the download folder ([21a9d32](https://github.com/Reaparr/Reaparr/commit/21a9d32a2acbdd03796b7eb3a9dac2482b759d6f))
* **Web-UI:** fixed typecheck and eslint errors ([5397b0d](https://github.com/Reaparr/Reaparr/commit/5397b0d91019c0011342ebaf8287a28895d35b09))
* **WebAPI:** Generate Test Database first before setting up Autofac container ([243dd0b](https://github.com/Reaparr/Reaparr/commit/243dd0b18a587fbf1e8cd6a5450b59bd2688c691))
* **WebAPI:** possible fix for inaccurate download speed calculation ([42ef74a](https://github.com/Reaparr/Reaparr/commit/42ef74ab0466e8d79663975fa46d603377fd02d2))
* **WebAPI:** Remove unneeded update of updating the DownloadWorkerTasks when merging files ([c9246a2](https://github.com/Reaparr/Reaparr/commit/c9246a29bb1e7cdb708561fe432a357931d2afb5))


### Features

* **Web-UI:** Added a new background activity dialog when the Plex library media is syncing ([c5269bd](https://github.com/Reaparr/Reaparr/commit/c5269bda84b38630759c7f99a2acbc83d4977e2b))
* **Web-UI:** Display a different version in the WebUI when using the development build ([9d50ab2](https://github.com/Reaparr/Reaparr/commit/9d50ab214afb5ae6f11e739b9684e3774ec4976d))
* **WebAPI:** Shutting down the docker container will pause the active downloads ([2e8a788](https://github.com/Reaparr/Reaparr/commit/2e8a78813924fadee8bea025e62778b32a136c4b))


### Performance Improvements

* **Web-UI:** fixed a memory leak that was creating way to many routes for Cypress to work ([3c725c8](https://github.com/Reaparr/Reaparr/commit/3c725c85671b2915ed6bc41b3db667ed69e8a3de))


### Reverts

* **Web-UI:** revert setting transparent background to !important as it messes up other coloring ([8c8c599](https://github.com/Reaparr/Reaparr/commit/8c8c5997069e9fae437b88f2be6dbd7afe9261f5))

# [0.26.0](https://github.com/Reaparr/Reaparr/compare/v0.25.0...v0.26.0) (2024-11-26)


### Bug Fixes

* **Web-UI:** Fixed download selection not triggering when selecting a tv show episode ([d726e50](https://github.com/Reaparr/Reaparr/commit/d726e5035457a0d123c442e238f8b61966056553))
* **Web-UI:** Fixed missing scrollbar when viewing many download tasks on the download page ([7afecc4](https://github.com/Reaparr/Reaparr/commit/7afecc41affbf5cbccfe78cbe2c5a814c2b97334))
* **Web-UI:** Fixed the media selection dialog not closing after setting the selection ([746d700](https://github.com/Reaparr/Reaparr/commit/746d700f00984b844de3aa6b7c11a9c007c4177d))


### Features

* **Web-UI:** Added discord invite dialog which is shown once ([73a9ab1](https://github.com/Reaparr/Reaparr/commit/73a9ab1f04cfa8fbf1cce6687a6bd141293585ee))

# [0.25.0](https://github.com/Reaparr/Reaparr/compare/v0.24.0...v0.25.0) (2024-11-21)


### Bug Fixes

* **Web-UI:** Added missing text when there is no background activity ([b9ec0e6](https://github.com/Reaparr/Reaparr/commit/b9ec0e678e059d88f1113174ff227932099338ac))
* **Web-UI:** cleaned up the styling of the download speed limit slider and input ([4e2da9e](https://github.com/Reaparr/Reaparr/commit/4e2da9e08b23467620f90854bd3f19045fd6a907))
* **WebAPI:** Custom connection that are added will not be deleted when the server is retrieving connections from the PlexAPI ([785d6c6](https://github.com/Reaparr/Reaparr/commit/785d6c66d4ec39bd7bf56807c662c05cc13d4b94))
* **WebAPI:** Don't refresh and sync library media when no connection can be established ([51508a9](https://github.com/Reaparr/Reaparr/commit/51508a96f3d0fc67d90ca024b4b7fc901e2204ec))
* **WebAPI:** Fixed an issue where dupplicate connections for PlexServers would all be discarded instead of at least one server being that connection assigned ([5221d5b](https://github.com/Reaparr/Reaparr/commit/5221d5bf5b8a4b78cac2cb0726cfdec23da5f148))
* **WebAPI:** Fixed an issue where missing properties that were marked required when deserializing JSON would throw exceptions, now it will use the default value set without exceptions ([97c450e](https://github.com/Reaparr/Reaparr/commit/97c450eb474bc8304744fc6ddd05578e19161c1e))
* **Web-UI:** Fixed an issue where the root selection box would not work on the tv show detail page until a child selection had been made ([3cf237a](https://github.com/Reaparr/Reaparr/commit/3cf237a963cb2336afb1273c560c8886f305eb16))
* **Web-UI:** Fixed auto scroll to last item viewed not working when viewing all TvShows ([9e91b22](https://github.com/Reaparr/Reaparr/commit/9e91b22539141182a62f2d988421810fb3843f38))
* **Web-UI:** Fixed bug where when a server has no-connections that it would be displayed as if it did have in the checkServerConnectionsDialog ([2dc7e01](https://github.com/Reaparr/Reaparr/commit/2dc7e010e4f531483a82a7e0097654fff7741433))
* **Web-UI:** Fixed changed job name causing the connections progress dialog to not show ([016fac2](https://github.com/Reaparr/Reaparr/commit/016fac27784e45b2ec6699a1eb5a71b6ebbe286b))
* **WebAPI:** Fixed inaccurate downloadspeed, it now uses the last few seconds as a metric instead of the entire download duration ([b634756](https://github.com/Reaparr/Reaparr/commit/b6347569a502317aea90ebdc162238bf9f425713))
* **WebAPI:** Fixed missing plex token to retrieve media poster when viewing tv show detail ([479924e](https://github.com/Reaparr/Reaparr/commit/479924ecbdaa4bda9931eb97f934554c82dcace7))
* **Web-UI:** Fixed missing scrolling bars when opening a tv show detail page and opening the season overview ([6d1fc03](https://github.com/Reaparr/Reaparr/commit/6d1fc038c1f43a99a19f109fb50f6a7c6f9a3d53))
* **WebAPI:** Fixed not retuning the next download task when it is downloading ([cd9861a](https://github.com/Reaparr/Reaparr/commit/cd9861a30ba2dd6f52c706c3ab0d89a14f8c51f9))
* **WebAPI:** Fixed the DownloadQueue not correctly determining if a server is online ([7794792](https://github.com/Reaparr/Reaparr/commit/7794792d579617d16ba5ad4835becc323dec57f3))
* **WebAPI:** Fixed the endpoint for hiding notifications ([726e54c](https://github.com/Reaparr/Reaparr/commit/726e54c8c1add58236cd14f749edb7f59bb53177))
* **Web-UI:** Fixed the incorrect help text for the Re-sync media command in the server settings ([1ee526c](https://github.com/Reaparr/Reaparr/commit/1ee526c463676233527399c8edbb85a694506a05))
* **WebAPI:** Fixed the incorrect sortIndex when requesting all media ([58a82cb](https://github.com/Reaparr/Reaparr/commit/58a82cbffe74ee0af43d1139a95b6f941b43b19f))
* **Web-UI:** Fixed the jumpy effect when loading the page with all the media posters which goes from 1 column to the correct width ([ee4ed8f](https://github.com/Reaparr/Reaparr/commit/ee4ed8f298aa31bfd4f5b7239e433e21b1edb7b1))
* **Web-UI:** Fixed the LanguageSelect size ([444a680](https://github.com/Reaparr/Reaparr/commit/444a6802fe20b72808ea30282ce3fa756cb33876))
* **Web-UI:** Fixed the low resolution of media images in the posters ([ad498b5](https://github.com/Reaparr/Reaparr/commit/ad498b5dc19a06b26d9acdf8f23a564531747983))
* **Web-UI:** Fixed the missing background when viewing a tv-show detail ([a28951d](https://github.com/Reaparr/Reaparr/commit/a28951d98c30ce981512585d4078eddd97833d0e))
* **Web-UI:** Fixed the notification drawer visibility and displaying notifications ([417b5b7](https://github.com/Reaparr/Reaparr/commit/417b5b79fae5b3b9560b0199b5c8bad5f50e56c3))
* **WebAPI:** Fixed the Plex account setup process failing when there is a server with no connections ([88504cf](https://github.com/Reaparr/Reaparr/commit/88504cfc3a5ba48053e55d61d87d98702cc269d2))
* **WebAPI:** Fixed the stopped status for a DownloadWorker not propagated properly due to premature termination and disposal ([fdce761](https://github.com/Reaparr/Reaparr/commit/fdce7611c989897a0b7f6c8f6cf08aaa00af7190))
* **Web-UI:** Fixed the ugly check background when clicking the view button in the media overview ([4462274](https://github.com/Reaparr/Reaparr/commit/446227487c561c796348e38e5b7ba88a59880423))
* **WebAPI:** Fixed the verificationCode initialized to null when returning PlexAccounts from the API ([3bc1a04](https://github.com/Reaparr/Reaparr/commit/3bc1a0435e93e62c5432c29d6dcddaf2cda87542))
* **Web-UI:** Fixed typescript errors ([0f8c8cb](https://github.com/Reaparr/Reaparr/commit/0f8c8cbcf002e9908f63693ba08ddc3a9c652026))
* **Web-UI:** Lot of polish fixes to the detail page for tv show media ([2e4c336](https://github.com/Reaparr/Reaparr/commit/2e4c336d084ab6358f02c4faf50c1376061a7b86))
* **Web-UI:** Prevent unneeded all media overview refresh when no media options have changed in the dialog on close ([ec1a613](https://github.com/Reaparr/Reaparr/commit/ec1a6134d5838ffd110679f913deb1480734f6af))
* **WebAPI:** The IsOwned on a server is now determined if there is an PlexAccount setup that is the owner ([f04f9bc](https://github.com/Reaparr/Reaparr/commit/f04f9bc6890fa242186c5bb85f1a8b894ff956ed))


### Features

* **Web-UI:** Add media switch for the overview page by clicking on the header title ([305a96e](https://github.com/Reaparr/Reaparr/commit/305a96e30c9704c32cfdd0916942675262af8e9e))
* **Web-UI:** Added background activity button to the appbar and removed the background animation toggle button ([c3adaf3](https://github.com/Reaparr/Reaparr/commit/c3adaf30efc0dd39b133811f4111a02d12921e3a))
* **Web-UI:** Added button in the Plex server settings to create custom connections to connect to a Plex server ([65c792e](https://github.com/Reaparr/Reaparr/commit/65c792ee82381b20ea073c36e7b17ed430c3d5db))
* **Web-UI:** Added button to freely generate tokens in the Account dialog when an account has a username and password authentication ([87a822e](https://github.com/Reaparr/Reaparr/commit/87a822e50deb3b2181db2ef9d49fc39f3c39ab2a))
* **WebAPI:** Added CreateConnectionEndpoint ([64a07eb](https://github.com/Reaparr/Reaparr/commit/64a07eb8bcad0cfd57b0765491303604cae70ada))
* **WebAPI:** Added endpoint for generating tokens for Plex accounts ([a67da40](https://github.com/Reaparr/Reaparr/commit/a67da40a16b894c9aac582cfb01558fb75f5f575))
* **WebAPI:** Added endpoints for updating and deleting Plex connections ([4d9a8ed](https://github.com/Reaparr/Reaparr/commit/4d9a8ed6779f0669713917b0320e376c89aa2d96))
* **Web-UI:** Added media options button to the media overview ([faf9e55](https://github.com/Reaparr/Reaparr/commit/faf9e5575e33eb6fb73829c6e5214efd34d77b35))
* **Web-UI:** Added media overview filter options to hide media from owned servers and from offline libraries ([fd49fd4](https://github.com/Reaparr/Reaparr/commit/fd49fd4d43b0df3f130441745a15c498dd05772c))
* **Web-UI:** Added menu to the activity button of running jobs ([b4ec627](https://github.com/Reaparr/Reaparr/commit/b4ec6277b118efad0717c7299685727c77ad54fd))
* **WebAPI:** Added new endpoint for retrieving all media of all servers to have 1 big overview ([b3f5f7d](https://github.com/Reaparr/Reaparr/commit/b3f5f7d6b1e4db9795d9901ce5f2f938cee8279b))
* **Web-UI:** Added override for data types in Cypress tests ([3a53c0e](https://github.com/Reaparr/Reaparr/commit/3a53c0e8f073857d7767d3919a5e9324be2eea03))
* **Web-UI:** Added Server Unreachable status to a download when the server goes down while downloading ([1f8bd86](https://github.com/Reaparr/Reaparr/commit/1f8bd86c92351cde3968de9dd559379610d37ab4))
* **Web-UI:** Added the quality of the episode in the tv show detail overview ([33c1080](https://github.com/Reaparr/Reaparr/commit/33c1080fb0108364bf2e7813726f332fe8706063))
* **Web-UI:** Added validation check to prevent adding duplicate connection urls when it already exists for a different server ([18a4d4d](https://github.com/Reaparr/Reaparr/commit/18a4d4d6ab9897d8b27680c8dc1984f4b70823ab))
* **WebAPI:** Added validation endpoint for a server connection url to ensure that it works ([14c3121](https://github.com/Reaparr/Reaparr/commit/14c3121cb180e28fb119cdbf4f5aca30db466cc5))
* **Web-UI:** Added Webstorm Nuxt Debug run profile ([627a85f](https://github.com/Reaparr/Reaparr/commit/627a85fdfe79514c2a209bfe51d1207fdcd74f92))
* **Web-UI:** All dialogs are now much more size responsive and work through the power of CSS grids ([5067225](https://github.com/Reaparr/Reaparr/commit/5067225b5f543e94d2506472a9eb137211a59d65))
* **Web-UI:** Allow the editing and deletion of connections in the server settings ([55b714f](https://github.com/Reaparr/Reaparr/commit/55b714fcca3382c94c0545b93e0da3e1ad67d69f))
* **Web-UI:** Polish the media statistics display in the media overview bar ([fdebd9b](https://github.com/Reaparr/Reaparr/commit/fdebd9b5d90b7a5bcaf7622c5f4a50b67380e94c))
* **Web-UI:** The question if you want to start the setup process is now a dialog pop-up window ([6f42ee9](https://github.com/Reaparr/Reaparr/commit/6f42ee9377cf19ce78d33306d0d4122e498dc78f))
* **Web-UI:** Toggling the media overview type is now saved when going to the home page ([20d9d83](https://github.com/Reaparr/Reaparr/commit/20d9d83958a8601bf268f5fabe8b25d19eb6f6fa))
* **WebAPI:** When a DownloadTask is stopped due to offline server, it will now auto-resume when the server is back-online ([63abda9](https://github.com/Reaparr/Reaparr/commit/63abda9f240395b12e6d6a04ddf55cb6440a2619))


### Performance Improvements

* **WebAPI:** Calculate media list statistics in the back-end before sending it to the front-end ([f521702](https://github.com/Reaparr/Reaparr/commit/f52170245be57f8166c9cd32d2b76501e63fd638))
* **WebAPI:** Further slight improvements to performance with sorting of querying media ([48f3500](https://github.com/Reaparr/Reaparr/commit/48f35004562a1c3f6d044adad6cca1906a0e1eb7))
* **WebAPI:** Improved performance of querying media by projecting entity to DTO and moving the thumbnail url compiling to the front-end ([271d0d0](https://github.com/Reaparr/Reaparr/commit/271d0d0b9393c5b7883da9a2c2e11e779a131017))
* **Web-UI:** the full thumbnail url is now compiled in the front-end since that data is already sent beforehand ([2b9535f](https://github.com/Reaparr/Reaparr/commit/2b9535f87dcf1dee7790024993b3fb743b342ac4))

# [0.24.0](https://github.com/Reaparr/Reaparr/compare/v0.23.2...v0.24.0) (2024-10-26)


### Bug Fixes

* **WebAPI:** Added an IsServerOnline check before picking the next downloadTask to download ([1e9f67f](https://github.com/Reaparr/Reaparr/commit/1e9f67fe78ad1feb13e21ad2b0051824bc5082b0))
* **WebAPI:** Correctly configured CORS to only allow same-origin requests to the server ([e90bfb9](https://github.com/Reaparr/Reaparr/commit/e90bfb9480281d3d9e85ce77e3b0d27571f5a9bc))
* **Web-UI:** Display fallback image when the poster thumbnail cannot be loaded ([dcb77f2](https://github.com/Reaparr/Reaparr/commit/dcb77f2773150a9beb60b670c4cfcce9acfd08dc))
* **WebAPI:** Fix Result.Value exception when accessed in failed mode ([69da8d0](https://github.com/Reaparr/Reaparr/commit/69da8d0b4ae838512922c1dd62a7bca0e4262a21))
* **WebAPI:** Fixed a bug where PlexServers that have the same (local) connections are causing media/libraries to be messed up. These are now filtered out ([c7afef8](https://github.com/Reaparr/Reaparr/commit/c7afef8c16b648fda6947d6cbaef5523e4bf7e4c))
* **WebAPI:** Fixed account data that couldn't be saved when in tokenMode due to validation restrictions ([e34eb5b](https://github.com/Reaparr/Reaparr/commit/e34eb5b1048871ba68e5fbd3058d7f8cb462d510))
* **WebAPI:** Fixed exception when no valid connection could be found and the thumbnail link had to be generated ([2482e12](https://github.com/Reaparr/Reaparr/commit/2482e122ad11d805d2a881b09ae133d773be0eca))
* **Web-UI:** Fixed going back when viewing a tv show to the media overview not loading the last page correctly ([7ebbbb2](https://github.com/Reaparr/Reaparr/commit/7ebbbb2e525400d2c17ae9212794b2d203da74b3))
* **WebAPI:** Fixed incorrect index numbers appearing next to media ([e1b3cf0](https://github.com/Reaparr/Reaparr/commit/e1b3cf07fefebb11d6a8d6810fcb697250da8edd))
* **WebAPI:** Fixed life cycle hooks not being triggered by a container shutdown ([3b6b2bd](https://github.com/Reaparr/Reaparr/commit/3b6b2bd05b26b66eb61539145e24af369248b783))
* **Web-UI:** Fixed missing default media poster when media has no thumbnail ([4bf3ea8](https://github.com/Reaparr/Reaparr/commit/4bf3ea8adbe4ab84df17775d2a4184ab57b32170))
* **WebAPI:** Fixed out or range unix time errors by parsing long from milliseconds instead of seconds ([dd66e9d](https://github.com/Reaparr/Reaparr/commit/dd66e9d4846f1c19e1e3326c5f7079407f9baf22))
* **WebAPI:** Fixed signalR not working due to wrong order of initialization ([bc87ef0](https://github.com/Reaparr/Reaparr/commit/bc87ef026d41c8f70af1a51c1c62650a4a376ae9))
* **Web-UI:** Fixed the download segments value being half-way hidden due to UI cut-off ([d348b27](https://github.com/Reaparr/Reaparr/commit/d348b273bdffa1d2828ae87e671c02cfff617874))
* **WebAPI:** fixed the download task not pausing when not actually downloading ([7f9d287](https://github.com/Reaparr/Reaparr/commit/7f9d28745dcc3006a30cc1b6eabb604918c9449a))
* **Web-UI:** Fixed the highlight effect not showing when sometimes navigating with the alphabet navigation ([087c1de](https://github.com/Reaparr/Reaparr/commit/087c1ded8a9e9c631b49abc6a30bd712bf8c2586))
* **Web-UI:** Fixed the missing styling of the scrollbar in firefox ([3610ccf](https://github.com/Reaparr/Reaparr/commit/3610ccffeb8421b51fff0faef1ceda5162153e98))
* **WebAPI:** fixed the PlexAPI not returning unauthorized error when the token has become invalid ([f3604fb](https://github.com/Reaparr/Reaparr/commit/f3604fb975a564e36696d62139aa0d7bdc837382))
* **WebAPI:** Fixed the sortIndex not set on TvShows when syncing media ([bc1cb79](https://github.com/Reaparr/Reaparr/commit/bc1cb799cccebb874ac905a94d0f0977ef3a4c42))
* **Web-UI:** Folderpaths can now be edited freely even when download tasks are active, changes will now apply to downloadtask that have to start ([fe2d80e](https://github.com/Reaparr/Reaparr/commit/fe2d80ea3f04796a614baf4a8e487295d99de157))
* **WebAPI:** hopeful fix that adress files to always be created as root ([5a03cb6](https://github.com/Reaparr/Reaparr/commit/5a03cb6087af0df3978f62a857e57a10740e153f))
* **WebAPI:** Only a single copy of the server status will now be kept in the database, this makes getting the latest status much easier and any online checks ([8f9dac1](https://github.com/Reaparr/Reaparr/commit/8f9dac165cd2b8bb4e84d87f36312079f47d0443))
* **WebAPI:** Possible fix that should resolve SQLite Error 5: 'database is locked'. by waiting until the database is unlocked ([0c1151a](https://github.com/Reaparr/Reaparr/commit/0c1151a9175b43f600cf631fb66d77c731836dd7))
* **WebAPI:** Quartz crash when database has to be created ([d7e81de](https://github.com/Reaparr/Reaparr/commit/d7e81de36889be0d43115cf5a265deb160ca25ae))
* **Web-UI:** Remove unneeded scrollbar on the page in firefox ([bb81824](https://github.com/Reaparr/Reaparr/commit/bb818246b519548eb361fd55a7bd3a4c5c59d100))
* **WebAPI:** set stj serializer in quartz test setup ([6badce7](https://github.com/Reaparr/Reaparr/commit/6badce74f9bea0f274008dfada9904b1a9ce3ba1))
* **WebAPI:** Some Plex Libraries have some random invalid GUID so were going to save it as a string in the database instead of a GUID ([a6f92c1](https://github.com/Reaparr/Reaparr/commit/a6f92c163d85778dc997cdb58a516aa61dd9ada3))
* **WebAPI:** unset default timeout, this might conflict with CommandTimeout ([b1ba28b](https://github.com/Reaparr/Reaparr/commit/b1ba28b32149356af16bcda032f9f8bf0d7e25db))


### Features

* **Web-UI:** Add tv-show download button on the media poster to quick download ([945e836](https://github.com/Reaparr/Reaparr/commit/945e836a68317f00cd851bc33f44d857b24fa0d0))
* **Web-UI:** Added a search feature to libraries which will search within that library. More general search will come later ([7e573f8](https://github.com/Reaparr/Reaparr/commit/7e573f8ee8482f26289c1b186faf12d67f1710c1))
* **WebAPI:** Added failsafe that shutsdown the container when the database cannot be setup ([55c47af](https://github.com/Reaparr/Reaparr/commit/55c47afc48a341f551a6506a5b1e3f383f2931dd))
* **WebAPI:** Added method to easily mock and set up a httpclient for unit tests ([3f26d93](https://github.com/Reaparr/Reaparr/commit/3f26d939be6ee46abc52f34d572e30a82a44c07f))
* **Web-UI:** Added missing pages for photos and unknown to display that they are not supported yet ([e789ad2](https://github.com/Reaparr/Reaparr/commit/e789ad2521f83778d9b9b38aa5c207d8b92c6019))
* **WebAPI:** Added UNMASKED env variable to unmask sensitive data in the logs ([41ff0ae](https://github.com/Reaparr/Reaparr/commit/41ff0ae90974e6b1c65085f11e92933e011d791b))
* **Web-UI:** keep retrying to reconnect with signalr when connection is lost due to offline back-end ([70ccbf1](https://github.com/Reaparr/Reaparr/commit/70ccbf1a581de3c36e793a4a91704a40cb0a78cc))
* **Web-UI:** keep retrying to reconnect with signalr when connection is lost due to offline back-end ([bd5a0c3](https://github.com/Reaparr/Reaparr/commit/bd5a0c3fe2ac0037cfa7f788cbfc7781c8880f3b))
* **WebAPI:** Plex server status online/offline is now checked every 5 minutes ([c4ccf39](https://github.com/Reaparr/Reaparr/commit/c4ccf39f0128aff456082f4914a503dde2931374))
* **WebAPI:** set auto status checker to 10 minutes ([b5f0308](https://github.com/Reaparr/Reaparr/commit/b5f03089a334f5220d1efaec5168de29578f89a7))
* **Web-UI:** The library media icon will now flash when the library is syncing, stay grey when it has not been synced yet and turned white when it has been synced of its media ([8df1304](https://github.com/Reaparr/Reaparr/commit/8df13047229fd893e9c1a490abd9b26bc5ac5405))


### Performance Improvements

* **Web-UI:** Major navigation performance improvement when using the alphabet navigation to the right ([273eef8](https://github.com/Reaparr/Reaparr/commit/273eef86a915d479f185b2ff947f952e9e2f1403))
* **WebAPI:** minor performance improvements with big databases, stop searching for more media when the media count has been reached ([075aa69](https://github.com/Reaparr/Reaparr/commit/075aa698ba51a0510e745319d2fc1f54296d15d1))
* **WebAPI:** Sorting now happens once when the library media is updated which is then stored as an index, which should be a big performance upgrade and save diskspace ([1676baa](https://github.com/Reaparr/Reaparr/commit/1676baa4e6ed41ffa9aaa8ad7aac6f810a58908a))


### Reverts

* Revert "Update Dockerfile" ([9f2738d](https://github.com/Reaparr/Reaparr/commit/9f2738d02611c9bb14efd1e7310a8b8321615870))

## [0.23.2](https://github.com/Reaparr/Reaparr/compare/v0.23.1...v0.23.2) (2024-10-03)


### Bug Fixes

* **WebAPI:** added retry on timeout to the Plex api client ([baed12d](https://github.com/Reaparr/Reaparr/commit/baed12d5796e89f070c9c36cf4158f1849ae6b09))
* **WebAPI:** remove database transactions when writing media data, this might have been causing locks to the database ([cf641ec](https://github.com/Reaparr/Reaparr/commit/cf641ec748a09f7178b47024575eb0ff7205da44))


### Performance Improvements

* **WebAPI:** Enable sqlite connection pooling and increase the busy time-out to hopefully solve database locks ([1deb3b5](https://github.com/Reaparr/Reaparr/commit/1deb3b57d241cbdf18557528dad9d2551e69ba49))
* **WebAPI:** Greatly increased performance of opening a TvShow ([2567b98](https://github.com/Reaparr/Reaparr/commit/2567b98e7a4a5bc8710271076e515d2203236e0f))

## [0.23.1](https://github.com/Reaparr/Reaparr/compare/v0.23.0...v0.23.1) (2024-10-03)


### Bug Fixes

* **WebAPI:** Fixed exception when syncing certain libraries ([c716bae](https://github.com/Reaparr/Reaparr/commit/c716baeb142475a41ca84ca437266cecf25fbed2))
* **WebAPI:** Fixed movie libraries not syncing ([4eb55f0](https://github.com/Reaparr/Reaparr/commit/4eb55f027fe03fd64b2dd75be30392474b4ab257))
* **Web-UI:** fixed the total steps displayed when refreshing a library to be the correct number based on the library type ([99c1fcd](https://github.com/Reaparr/Reaparr/commit/99c1fcd6fbc332242010a7dbb860ec2711c68da3))
* **Web-UI:** fixed the ugly empty space underneath tv-show posters ([1c7d2bf](https://github.com/Reaparr/Reaparr/commit/1c7d2bf1439378ee2b2743d375d0943a7cb6aa0f))
* **Web-UI:** fixed typecheck errors ([8961da5](https://github.com/Reaparr/Reaparr/commit/8961da54f2205f233d64d13c31c6971889cf0a5a))

# [0.23.0](https://github.com/Reaparr/Reaparr/compare/v0.22.0...v0.23.0) (2024-10-03)


### Bug Fixes

* **Web-UI:** Added better error messages to the refreshing library page ([3029e5d](https://github.com/Reaparr/Reaparr/commit/3029e5deafa463a41585b5e80cc1960a2639575a))
* **WebAPI:** Fixed an issue where certain users could not log in due to serialization issues ([4f3019d](https://github.com/Reaparr/Reaparr/commit/4f3019df20b47fea14cce1a0cd2afffd517f11b3))
* **WebAPI:** Fixed empty PlexLibraries causing errors, being empty is valid ([a69a6e7](https://github.com/Reaparr/Reaparr/commit/a69a6e73c4d8d9bd0a17b3648935d3da46a223bf))
* **WebAPI:** fixed isRefresh always being false when sent in the library progress ([df54fdd](https://github.com/Reaparr/Reaparr/commit/df54fddf8cd76c675f5d4bfe89b3ff9e709816a9))
* **Web-UI:** fixed layout issue in dialogs ([4e1d56f](https://github.com/Reaparr/Reaparr/commit/4e1d56fa0e17f41bbb2bed9a47cc6aab11c5697a))
* **WebAPI:** Fixed several exceptions due to invalid json parse ([0b74ecc](https://github.com/Reaparr/Reaparr/commit/0b74ecc3e0ba53d53225fcc02d5a9407d6ddb9f2))
* **Web-UI:** Fixed typescript errors ([78948b1](https://github.com/Reaparr/Reaparr/commit/78948b15207fcde9c7f3f9c9a6e83dc7fff72fa6))


### Features

* **WebAPI:** Added a time remaining to sync data from the Plex API ([1b3923f](https://github.com/Reaparr/Reaparr/commit/1b3923f84db357e66458b89a125d240813d3152b))
* **Web-UI:** Added countdown to the library refresh page ([551ef8b](https://github.com/Reaparr/Reaparr/commit/551ef8bb6573596f0e893f78a5b6c256413777ee))
* **Web-UI:** Added the steps of the refresh process to the time remaining ([2b8057b](https://github.com/Reaparr/Reaparr/commit/2b8057b8edda53fdc7a90e83cfc125a4ce3465f4))


### Performance Improvements

* **WebAPI:** Big performance improvement on the merging of media data coming from the Plex API ([6154a22](https://github.com/Reaparr/Reaparr/commit/6154a22ed21ddb009369e80f367f243d68d12454))
* **Web-UI:** Huge performance improvement when loading huge libraries, no more crashing UI! ([6c0e7f7](https://github.com/Reaparr/Reaparr/commit/6c0e7f712c98e33d225f34fba1b821bcfc862358))
* **WebAPI:** Major performance improvement of writing tv shows to the database, 20min -> 2 seconds ([00be9d5](https://github.com/Reaparr/Reaparr/commit/00be9d59b7590673af6d8a934a988247fa0741f7))
* **WebAPI:** Start syncing media data  when all connections fora server have been tested ([afc0e21](https://github.com/Reaparr/Reaparr/commit/afc0e210d9b0db0b21c55684b8942505c9297c6a))

# [0.22.0](https://github.com/Reaparr/Reaparr/compare/v0.21.0...v0.22.0) (2024-09-30)


### Bug Fixes

* **WebAPI:** Added ways to catch exceptions from Plex.SDK ([21b4e2d](https://github.com/Reaparr/Reaparr/commit/21b4e2dfe5e24bbc6047cac8343fcb8d06115535))
* **Web-UI:** corrected spacing ([3e98ff4](https://github.com/Reaparr/Reaparr/commit/3e98ff4c0ddb783d8cc9665f20130472d5d78916))
* **Web-UI:** corrected vertical alignment of the title and media icon in the details dialog ([7350f87](https://github.com/Reaparr/Reaparr/commit/7350f87107f8a42c228bf0fb099e7a74f9819ae4))
* **WebAPI:** Fix full windows path names being used as a filename for media files ([a51d078](https://github.com/Reaparr/Reaparr/commit/a51d078baf2ba521dc39b6c7f464399d6e446f18))
* **WebAPI:** Fix minimum logLevel in serilog not being honored ([c039973](https://github.com/Reaparr/Reaparr/commit/c039973670897af3ddb31ed096234b7df1ce6b35))
* **WebAPI:** Fixed an issue where a PlexServer could time-out because Reaparr requested too much data at once, this is now in batches of 500tv shows per request ([59e0e07](https://github.com/Reaparr/Reaparr/commit/59e0e07b4ea2ed32b5bfa3831225242cfaf9ca13))
* **WebAPI:** Fixed default destination for libraries not being able to change from the UI ([a8aaea2](https://github.com/Reaparr/Reaparr/commit/a8aaea2145794843ee2bd78c8ffed5fda5168c48))
* **WebAPI:** Fixed exceptions not catched correctly when interacting with the Plex API SDK ([c2c5e91](https://github.com/Reaparr/Reaparr/commit/c2c5e914542e4fd6629ddcdee320f3ada2e199d6))
* **Web-UI:** fixed homepage alignment ([dde11ce](https://github.com/Reaparr/Reaparr/commit/dde11cef245f098b6df1f728e30f89e99113ccd1))
* **WebAPI:** fixed missing destination and download path from the download task details dialog ([c0afb3f](https://github.com/Reaparr/Reaparr/commit/c0afb3f9a5b598e9a8d594205f46ec8d16768092))
* **Web-UI:** Fixed missing translations for the download status and columns of various ui ([4567ed7](https://github.com/Reaparr/Reaparr/commit/4567ed720b91f29af8a6ff4fc89819464c490406))
* **WebAPI:** fixed the downdload queue breaking when repeatly pausing and resuming ([a2d277d](https://github.com/Reaparr/Reaparr/commit/a2d277dba75a97aa63604042952aa1f56cc23b64))
* **WebAPI:** Fixed the download commands not working when clicked on the parent, works for pause and stop now ([c5456bb](https://github.com/Reaparr/Reaparr/commit/c5456bbe55a049f52e45a01f35c8cd365aa85871))
* **WebAPI:** Fixed the download commands not working when clicked on the parent, works for start and resume now ([7925f62](https://github.com/Reaparr/Reaparr/commit/7925f623683deb3544f8ae0f72041309fff957c7))
* **WebAPI:** Fixed the download progress not resetting when a download task is stopped and the files are deleted ([87ad153](https://github.com/Reaparr/Reaparr/commit/87ad153afff9c95b81b706a9c77fb078e6600f45))
* **WebAPI:** Fixed the incorrect and slow progress updates from refreshing a library ([eaa54de](https://github.com/Reaparr/Reaparr/commit/eaa54deb94c645fce29956886e68f4fad9664b58))
* **Web-UI:** Fixed the incorrect display of connection text when testing a connection ([97e4d75](https://github.com/Reaparr/Reaparr/commit/97e4d75311632a0f2530a592c1df0c813839b1c2))
* **Web-UI:** Fixed the library bar with the refresh button being hidden when the library is empty ([0d32285](https://github.com/Reaparr/Reaparr/commit/0d3228520d0319fe92adf2bbee01836621e7e81f))
* **WebAPI:** Fixed the LOG_LEVEL env variablenot being respected for the backend logging ([238e50c](https://github.com/Reaparr/Reaparr/commit/238e50cf33fcaf28334a922000fb840e2bde799a))
* **Web-UI:** Fixed type errors ([520c2d7](https://github.com/Reaparr/Reaparr/commit/520c2d7d2d8cb6851816c782c36802cd751458da))
* **Web-UI:** Fixed type errors ([36b7131](https://github.com/Reaparr/Reaparr/commit/36b713187d6a14903429366f7cbe9b273f238c0b))
* **Web-UI:** fixed typecheck test ([8356c71](https://github.com/Reaparr/Reaparr/commit/8356c7132fcaab2feedc79232b6e865ec56ff216))
* **Web-UI:** fixed wrong translation in the download table column header ([b90be63](https://github.com/Reaparr/Reaparr/commit/b90be634ab6a6f1e6160103387def0e0e64dbe54))
* **Web-UI:** Made the title inside the refresh accounts menu more clear ([f9614d0](https://github.com/Reaparr/Reaparr/commit/f9614d007b5e3eaa38f828db116c7d5160e535b4))


### Features

* **WebAPI:** Added download task log endpoint ([dfffc47](https://github.com/Reaparr/Reaparr/commit/dfffc4740e16f7bf3426b68a0bd0cd21b34624a3))
* **Web-UI:** Added download task logs to the UI, see the details button on a download row and then click logs ([31edebd](https://github.com/Reaparr/Reaparr/commit/31edebda3c346fac5f5fe6a89eca728ef6d3ba63))


### Reverts

* **Web-UI:** hide search bar again until it's ready ([918fbc5](https://github.com/Reaparr/Reaparr/commit/918fbc5bac64a83f88f611b28832c8f5cb95a5ef))

# [0.21.0](https://github.com/Reaparr/Reaparr/compare/v0.20.0...v0.21.0) (2024-08-17)


### Bug Fixes

* **WebAPI:** Added missing FileMergeJob Type to JobTypes ([d5daea5](https://github.com/Reaparr/Reaparr/commit/d5daea5144a0c5944cf7b04ba458e201a91714f7))
* **Web-UI:** Fixed a PlexAccount not updating correctly in the UI ([e2b7abf](https://github.com/Reaparr/Reaparr/commit/e2b7abf9c96ecf12c2d3a33606f9cdedf156e21f))
* **WebAPI:** Fixed incorrect method of creating a Guid ([0062c2b](https://github.com/Reaparr/Reaparr/commit/0062c2b9e9b2338380c7f0ff601978d1475e7539))
* **WebAPI:** Fixed PlexLibraries incorrectly updating causing some to be missing ([3111183](https://github.com/Reaparr/Reaparr/commit/311118341f2714896bbffbf0752b4ce1ddbcbc30))
* **Web-UI:** Fixed the language flags being inconsistent ([0eee59e](https://github.com/Reaparr/Reaparr/commit/0eee59ef2094d8d1c665f5c04632d3b066bf51ca))
* **WebAPI:** Fixed the missing plexLibrary problem and the lack of Plexmedia ([1f64be2](https://github.com/Reaparr/Reaparr/commit/1f64be2db4f5654d5aad013167906e90a9c8676a))
* **WebAPI:** Fixed the PlexAccountId getting lost when validating ([7b9bc16](https://github.com/Reaparr/Reaparr/commit/7b9bc1626be89913e4ec9731658e72f674994b0b))
* **Web-UI:** Pre-translate missing translations for German and French ([fc1859c](https://github.com/Reaparr/Reaparr/commit/fc1859cdf299fdd9f997af3af3bd5d921fa92a35))


### Features

* **Web-UI:** Added Language Select to the setup page ([87d1287](https://github.com/Reaparr/Reaparr/commit/87d1287b396841adf3778b5e1db84b1e28efcc92))
* **Web-UI:** Added Polish language into babel-edit save file ([4ad23c8](https://github.com/Reaparr/Reaparr/commit/4ad23c8344d2f3c2b6f3dcef812e2636996f96c6))
* **Web-UI:** Added Polish language option ([d9ae110](https://github.com/Reaparr/Reaparr/commit/d9ae1108a0696b450293ca54d6f53049f41c739c))
* **Web-UI:** Disabling a PlexAccount will no also hide PlexServers from the sidebar when there are no accounts that have access anymore ([6a9dec3](https://github.com/Reaparr/Reaparr/commit/6a9dec3bcc890c904dcc99f357068b68bf21d572))

# [0.20.0](https://github.com/Reaparr/Reaparr/compare/v0.19.1...v0.20.0) (2024-08-12)


### Bug Fixes

* **WebAPI:** Fixed an issue where connections to PlexServer where not synced correctly with the database ([5bbc1d0](https://github.com/Reaparr/Reaparr/commit/5bbc1d025b429c5bce318259888845e7317a07b3))
* **Web-UI:** Fixed the percentages displayed being 100 times to high ([820b78a](https://github.com/Reaparr/Reaparr/commit/820b78a4fa3192ef4fbf14448585341c903b5e44))


### Features

* **Web-UI:** Better sort the Plex server connections when they are displayed ([bb031f8](https://github.com/Reaparr/Reaparr/commit/bb031f81681412f83745f275924f1f9249db9c2a))
* **WebAPI:** Hiding a PlexServer will now also disable it from use when executing operations ([5ff8796](https://github.com/Reaparr/Reaparr/commit/5ff8796ad3e4127f67ec15347212dbf4389028af))

## [0.19.1](https://github.com/Reaparr/Reaparr/compare/v0.19.0...v0.19.1) (2024-08-11)


### Bug Fixes

* **WebAPI:** Fixed a bug where the verification of MFA enabled accounts did not pop-up ([aa3c62c](https://github.com/Reaparr/Reaparr/commit/aa3c62caa6e8b9edb8cef8bbb9bcf8ecf10757db))

# [0.19.0](https://github.com/Reaparr/Reaparr/compare/v0.18.0...v0.19.0) (2024-08-10)


### Bug Fixes

* **WebAPI:** Fixed 401 errors not being returned from api endpoints ([8e76e03](https://github.com/Reaparr/Reaparr/commit/8e76e03fa7655d21d0cba329e070926f88dd4c6e))
* **WebAPI:** Fixed client devices being seen as a server, this is now checked and clients should not be added anymore ([9d576db](https://github.com/Reaparr/Reaparr/commit/9d576dbc1a8a77dc47f0d965c16cbdcc4de7cf17))
* **WebAPI:** Fixed the library metadata not being updated when syncing media ([e285ede](https://github.com/Reaparr/Reaparr/commit/e285edeedbef71a36eae3556d9c11de9dc0f2089))
* **Web-UI:** Made the url column for the check connections dialog wider ([286ca8a](https://github.com/Reaparr/Reaparr/commit/286ca8a91f66a9939dbfb7f7c22a7b233a485003))


### Features

* **Web-UI:** An plex.tv auth token can now be directly added in a account creation screen to avoid having to register with an username and password ([bff6ba4](https://github.com/Reaparr/Reaparr/commit/bff6ba4a0221d1839f166d5dc7ddc651b59d9672))
* **Web-UI:** Display the invalid token dialog when an invalid token has been given ([5d258df](https://github.com/Reaparr/Reaparr/commit/5d258df88438721af8309f5518eb8945614e0d39))

# [0.18.0](https://github.com/Reaparr/Reaparr/compare/v0.17.0...v0.18.0) (2024-08-09)


### Bug Fixes

* **Web-UI:** Cleanup the headers that were to big ([a576647](https://github.com/Reaparr/Reaparr/commit/a5766471ffe3587772642163ab22cb54d98a6752))
* **WebAPI:** Fixed a crash during library syncing where dupplicate keys were created in media ([fe13f2b](https://github.com/Reaparr/Reaparr/commit/fe13f2bb61891ae1f7bd31eb3d1ea92d74818729))
* **Web-UI:** Fixed broken percentage display due to missing translation ([9aa3167](https://github.com/Reaparr/Reaparr/commit/9aa3167e2c3056483a8d2403742a2efd7b1b284f))
* **Web-UI:** Fixed Cypress pathing by re setting the baseUrl ([bafe90d](https://github.com/Reaparr/Reaparr/commit/bafe90d6bfd5ec1a886146bbd1c7132dddc9f140))
* **Web-UI:** Fixed percentage that was 10000% ([58c0c15](https://github.com/Reaparr/Reaparr/commit/58c0c15b94e3040e41a4576f2ed6a5efc8b1cdb2))
* Fixed the duration of media beind incorrectly displayed due to milliseconds being treated as seconds ([78989a8](https://github.com/Reaparr/Reaparr/commit/78989a8955887a4132a60b3e3eb6cdfec8df2ef7))
* **WebAPI:** Fixed the library not being marked as synced, thus syncing media needlessly when there were no changes ([8a6cc00](https://github.com/Reaparr/Reaparr/commit/8a6cc00c6b85c981520c154ce363d565340db821))
* **Web-UI:** Fixed the missing server media sync progress bar when media is syncing ([5338729](https://github.com/Reaparr/Reaparr/commit/533872962c20fd232b536f549c80e7912480c3fd))
* **Web-UI:** Fixed warnings about unused props on QStatus ([dfae4b7](https://github.com/Reaparr/Reaparr/commit/dfae4b70e9e4771877968de5dae930b02d867125))


### Features

* **WebAPI:** Added places where a notification is sent to the front-end when data in the back-end is updated ([7c9bcda](https://github.com/Reaparr/Reaparr/commit/7c9bcdaa445f95355bc8027a590d2f8dd9ddd380))
* **WebAPI:** Store additional connections per plex server to allow plex.direct connections when a server has limited connectivity ([4b2f13c](https://github.com/Reaparr/Reaparr/commit/4b2f13ce14af2958ac744809f72a79c9e48156fe))


### Performance Improvements

* **WebAPI:** Refactored the RefreshLibraryAccessCommand to do multiple servers in parrallel ([6925dec](https://github.com/Reaparr/Reaparr/commit/6925decc81da63803a4d3fff5353a7a3e1220eb8))

# [0.18.0](https://github.com/Reaparr/Reaparr/compare/v0.17.0...v0.18.0) (2024-08-09)


### Bug Fixes

* **Web-UI:** Cleanup the headers that were to big ([a576647](https://github.com/Reaparr/Reaparr/commit/a5766471ffe3587772642163ab22cb54d98a6752))
* **WebAPI:** Fixed a crash during library syncing where dupplicate keys were created in media ([fe13f2b](https://github.com/Reaparr/Reaparr/commit/fe13f2bb61891ae1f7bd31eb3d1ea92d74818729))
* **Web-UI:** Fixed broken percentage display due to missing translation ([9aa3167](https://github.com/Reaparr/Reaparr/commit/9aa3167e2c3056483a8d2403742a2efd7b1b284f))
* **Web-UI:** Fixed Cypress pathing by re setting the baseUrl ([bafe90d](https://github.com/Reaparr/Reaparr/commit/bafe90d6bfd5ec1a886146bbd1c7132dddc9f140))
* **Web-UI:** Fixed percentage that was 10000% ([58c0c15](https://github.com/Reaparr/Reaparr/commit/58c0c15b94e3040e41a4576f2ed6a5efc8b1cdb2))
* Fixed the duration of media beind incorrectly displayed due to milliseconds being treated as seconds ([78989a8](https://github.com/Reaparr/Reaparr/commit/78989a8955887a4132a60b3e3eb6cdfec8df2ef7))
* **WebAPI:** Fixed the library not being marked as synced, thus syncing media needlessly when there were no changes ([8a6cc00](https://github.com/Reaparr/Reaparr/commit/8a6cc00c6b85c981520c154ce363d565340db821))
* **Web-UI:** Fixed the missing server media sync progress bar when media is syncing ([5338729](https://github.com/Reaparr/Reaparr/commit/533872962c20fd232b536f549c80e7912480c3fd))
* **Web-UI:** Fixed warnings about unused props on QStatus ([dfae4b7](https://github.com/Reaparr/Reaparr/commit/dfae4b70e9e4771877968de5dae930b02d867125))


### Features

* **WebAPI:** Added places where a notification is sent to the front-end when data in the back-end is updated ([7c9bcda](https://github.com/Reaparr/Reaparr/commit/7c9bcdaa445f95355bc8027a590d2f8dd9ddd380))
* **WebAPI:** Store additional connections per plex server to allow plex.direct connections when a server has limited connectivity ([4b2f13c](https://github.com/Reaparr/Reaparr/commit/4b2f13ce14af2958ac744809f72a79c9e48156fe))


### Performance Improvements

* **WebAPI:** Refactored the RefreshLibraryAccessCommand to do multiple servers in parrallel ([6925dec](https://github.com/Reaparr/Reaparr/commit/6925decc81da63803a4d3fff5353a7a3e1220eb8))

# [0.17.0](https://github.com/Reaparr/Reaparr/compare/v0.16.0...v0.17.0) (2024-08-06)


### Features

* Added new Reaparr logo with space around it ([8dbefaf](https://github.com/Reaparr/Reaparr/commit/8dbefaf9ac0c1285dda188710fb012693a8fde0f))

# [0.17.0](https://github.com/Reaparr/Reaparr/compare/v0.16.0...v0.17.0) (2024-08-06)


### Features

* Added new Reaparr logo with space around it ([8dbefaf](https://github.com/Reaparr/Reaparr/commit/8dbefaf9ac0c1285dda188710fb012693a8fde0f))

# [0.16.0](https://github.com/Reaparr/Reaparr/compare/v0.15.0...v0.16.0) (2024-08-02)


### Bug Fixes

* **Web-UI:** A server can now be hidden from the server sidebar by opening the server dialog and clicking hide ([8f45d65](https://github.com/Reaparr/Reaparr/commit/8f45d652f185dc022f80739dfa8fa412e5b5690f))
* **WebAPI:** Fixed a bug where if a authtoken failed to be retrieved then it would crash the media call ([c8f8a44](https://github.com/Reaparr/Reaparr/commit/c8f8a44fc09b84acac11ebab00be75a59dfb14ff))
* **WebAPI:** Fixed an issue where seperate injected settingsmodules were not reactive to the parent usersettings ([d54e5d9](https://github.com/Reaparr/Reaparr/commit/d54e5d9a9e7e6ef21f81f277321958924e397052))
* **Web-UI:** Fixed the download table selectAll checkbox not selecting its children ([8dd200b](https://github.com/Reaparr/Reaparr/commit/8dd200b68de7e3acf5d4eef38fc4e1d25280383d))
* **Web-UI:** Typecheck errors fix ([382bb9d](https://github.com/Reaparr/Reaparr/commit/382bb9d0fc5fc44cd9381624ceffbd308eb2864c))


### Features

* add hide/unhide server functionality in UI settings ([47b0862](https://github.com/Reaparr/Reaparr/commit/47b08626eda073855491838ff7692f4de841885c))
* **WebAPI:** Added an endpoint to hide a server from view ([10c2149](https://github.com/Reaparr/Reaparr/commit/10c2149c15c97d45442ea2c6fafd4b122fa52ece))
* **Web-UI:** Migrated prettier config to eslint ([63c7dce](https://github.com/Reaparr/Reaparr/commit/63c7dcec802855d495ddd92bba3c343a2fa5b699))


### Performance Improvements

* **WebAPI:** Made the instantiation of JsonSerializerOptions more performanat by reusing instances ([5780b39](https://github.com/Reaparr/Reaparr/commit/5780b39783d91ca5770725b4dfe1fe0566b4b1d4))

# [0.15.0](https://github.com/Reaparr/Reaparr/compare/v0.14.0...v0.15.0) (2024-07-29)


### Bug Fixes

* **WebAPI:** Added FluentValidation validator to the GenerateDownloadTaskHandlers ([fd8f52b](https://github.com/Reaparr/Reaparr/commit/fd8f52b1a97163ce678edaeb1174947b5644dbd6))
* **WebAPI:** Added migration for deleting the Data total property ([9bc82d4](https://github.com/Reaparr/Reaparr/commit/9bc82d4a1233a408b03e6259fcba5af763b72309))
* **Translations:** Added missing translations for the download details dialog ([b97cf8e](https://github.com/Reaparr/Reaparr/commit/b97cf8e6982cbb7efe38ca240866d11e19c47745))
* **Web-UI:** Added type to imports and fixed Primevue import ([dac5740](https://github.com/Reaparr/Reaparr/commit/dac574073408115ccf5362c6b52c94019d5fae0e))
* **WebAPI:** Allow an id to be set when creating a PlexAccount, this will now be set to 0 ([621ab66](https://github.com/Reaparr/Reaparr/commit/621ab66dbc6466f3ef4e140b80ef3d06020c0040))
* **Web-UI:** Disabled dark-mode toggle as the light theme is too broken ([98d06c7](https://github.com/Reaparr/Reaparr/commit/98d06c7b72ee5f5bdfc4bdee1f99621ab8e27546))
* **WebAPI:** Fix the active account access refresh ([627c27e](https://github.com/Reaparr/Reaparr/commit/627c27e24d7744bffaa8e6e389d782cb6357e86d))
* **Web-UI:** Fixed "this" in the various Pinia stores instead of correct actions/getters reference ([47f479e](https://github.com/Reaparr/Reaparr/commit/47f479e969d336dbe3bb06bb1e2748f5f0a33912))
* **Web-UI:** Fixed an extra scrollbar appearing ([25e48bc](https://github.com/Reaparr/Reaparr/commit/25e48bce0192eadfc315f0d596f322d5381ef42a))
* **WebAPI:** Fixed an issue where an invalid PlexServer token would not lead to an error state in a download ([9cae79b](https://github.com/Reaparr/Reaparr/commit/9cae79b8a3e29744f19e082b3e0e7bfa7adabefc))
* **Web-UI:** Fixed an issue where the folderPath could not be confirmed in the DirectoryBrowser ([5a1e571](https://github.com/Reaparr/Reaparr/commit/5a1e5718b6135707d35f1eb18ebd22c89b4923c2))
* **WebAPI:** Fixed an issue where UpdateDownloadProgress could update DownloadProgress of entities that do not have those properties in the database scheme ([db76f0f](https://github.com/Reaparr/Reaparr/commit/db76f0f09a5931bce773aaa163cc26ef7d24c41b))
* **Web-UI:** Fixed bug in mock data generation ([fc52a60](https://github.com/Reaparr/Reaparr/commit/fc52a60ce0bb85249d3709823df9ab626dfc9357))
* **Web-UI:** Fixed color styling of PrimeVue elements ([c993078](https://github.com/Reaparr/Reaparr/commit/c9930780d8f78d9fd01b03e18606482c36263d03))
* **Web-UI:** Fixed console error: Failed to load module script: Expected a JavaScript module script but the server responded with a MIME type of "text/html". Strict MIME type checking is enforced for module scripts per HTML spec. ([54accf7](https://github.com/Reaparr/Reaparr/commit/54accf7f40dd2741e9d2774cc49439ae70c615ef))
* **WebAPI:** Fixed conversion error in the JobStatusUpdateMapper ([a3b9500](https://github.com/Reaparr/Reaparr/commit/a3b95000adc4056d15862f5e6f6d7238f4172b22))
* **WebAPI:** Fixed DbContext error where the same context could be used while it was already in use by adding DownloadTaskWorkerLogNotification ([a6bfc84](https://github.com/Reaparr/Reaparr/commit/a6bfc84fad19a23b83808b2a0779e83a95aecbf4))
* **WebAPI:** Fixed DownloadPreviewMapper to work with query projection ([341ecc6](https://github.com/Reaparr/Reaparr/commit/341ecc6112c18aaed8712f420366530d603a6552))
* **WebAPI:** Fixed DownloadQueue repicking the same downloadTask when there are only DownloadFinished ([77c52f0](https://github.com/Reaparr/Reaparr/commit/77c52f0eb479b65ffafa73806b7191e845a6cdbb))
* **WebAPI:** Fixed endpoint exception ([6aa66a5](https://github.com/Reaparr/Reaparr/commit/6aa66a53f708444af0656371fdf739255fd8e099))
* **WebAPI:** Fixed enums being sent as integers instead of strings ([1185912](https://github.com/Reaparr/Reaparr/commit/1185912540d421b7ef7c3f173b25961fb002bb2c))
* **Web-UI:** Fixed error "Error: SyntaxError: Unexpected token 'export' " by adding "type="module"" ([9402322](https://github.com/Reaparr/Reaparr/commit/94023226f82513318009cb9e6a443ec72ecf501f))
* **WebAPI:** Fixed false postive for an username already in use ([3c7bd7d](https://github.com/Reaparr/Reaparr/commit/3c7bd7d47b850ae6c1059313161ce8cd3967c395))
* **WebAPI:** Fixed folderPath not saving correctly due to request validation ([cd06599](https://github.com/Reaparr/Reaparr/commit/cd065993f4c07a9d26c8edb9355bf3d531a196da))
* **WebAPI:** Fixed Integration tests boot due to WebAppication start process changing ([4525cd8](https://github.com/Reaparr/Reaparr/commit/4525cd8ce4b1cc29268d67a497e18f9d524b3b96))
* **WebAPI:** Fixed integration tests ([71218d4](https://github.com/Reaparr/Reaparr/commit/71218d496cd7bf9f2cbba9901e104020c78c7064))
* **WebAPI:** Fixed mapping issues when sending DownloadTasksDTO's to the front-end ([513b364](https://github.com/Reaparr/Reaparr/commit/513b364322e4fac7646fe5616036558181a37db1))
* **WebAPI:** Fixed missing sort title, now every piece of media will always have a sort title ([d9085ff](https://github.com/Reaparr/Reaparr/commit/d9085ff52a246bebc7421d2ccf5a021090d69dc0))
* **WebAPI:** Fixed missing Title and FullTitle for DownloadTaskFiles ([21ea4ac](https://github.com/Reaparr/Reaparr/commit/21ea4ac84564727ee57fb94d63d12df89a9c3ec1))
* **WebAPI:** Fixed old reference in dockerfile ([87ae521](https://github.com/Reaparr/Reaparr/commit/87ae5216bb16119947c7875e5dde5b9f959f7e15))
* **WebAPI:** Fixed old reference in unit test solution file to DownloadManager ([db3d3b5](https://github.com/Reaparr/Reaparr/commit/db3d3b5858c6ea9763eb7b21ec37e875b628e1a7))
* **WebAPI:** Fixed open api spec signatures of several endpoints ([5c4ef1f](https://github.com/Reaparr/Reaparr/commit/5c4ef1f5fb9508b2ec4be411f8bc01da0c40f353))
* **WebAPI:** Fixed parent DownloadTasks status update not being set ([e94f48d](https://github.com/Reaparr/Reaparr/commit/e94f48d2d12f462afa9fb5ef877c75c2855bfdce))
* **WebAPI:** Fixed plex account data not being cleaned up when the account is deleted ([aa0d04b](https://github.com/Reaparr/Reaparr/commit/aa0d04bb794b2706bf44d8bf85a064efd10f1619))
* **WebAPI:** Fixed PlexLibraryMapper mapping errors ([6803926](https://github.com/Reaparr/Reaparr/commit/68039262fd0143b41757b381a21b9a74c3fca6ec))
* **WebAPI:** Fixed possible null reference exception when refreshing a library media ([4e5b5b6](https://github.com/Reaparr/Reaparr/commit/4e5b5b601824e99248f3d74f829e82b75a6f11a9))
* **WebAPI:** Fixed several config issues in relation to fastendpoints and swagger ([b47ab1c](https://github.com/Reaparr/Reaparr/commit/b47ab1ceed02764b9f237b04c5f7404ef822adb9))
* **Web-UI:** Fixed the add button misalignment when adding a new FolderPath ([180c97d](https://github.com/Reaparr/Reaparr/commit/180c97d0a6fed81c079e4e07d677cdc90842e6d1))
* **Web-UI:** Fixed the alphabet navigation to displaying the correct letters based on the displayed media ([3453660](https://github.com/Reaparr/Reaparr/commit/3453660ff64259f2548904c2db2c568dde018799))
* **WebAPI:** Fixed the autofac registration for IReaparrBbContext ([9f9c7dd](https://github.com/Reaparr/Reaparr/commit/9f9c7dd9d0bd87e7d6656d577b9b6d9d96b6d772))
* **Web-UI:** Fixed the check server status button to check all connection at once ([c885fc9](https://github.com/Reaparr/Reaparr/commit/c885fc9fc4be249fd2d87bc4aca2eeb63d0e2943))
* **WebAPI:** Fixed the DbContext nog being available in Auto mock during unit testing ([ad2ad80](https://github.com/Reaparr/Reaparr/commit/ad2ad807fb4306f5594c74a44e6f1de75f3312d7))
* **WebAPI:** Fixed the delayed api call when setting up an account ([8bac31e](https://github.com/Reaparr/Reaparr/commit/8bac31e85081e0dfb41f343f641eeb17334f4f4e))
* **Web-UI:** Fixed the download bar commands not working with the selected downloadtasks in the downloadTable ([bc336ae](https://github.com/Reaparr/Reaparr/commit/bc336ae19b4af090ce581aebdd0567eec90c21d2))
* **Web-UI:** Fixed the download details dialog not opening when there are errors ([f519526](https://github.com/Reaparr/Reaparr/commit/f51952636be46d1f706f16ad8461f032b7bd1738))
* **WebAPI:** Fixed the download restart command not working ([e62b8e9](https://github.com/Reaparr/Reaparr/commit/e62b8e98b1c05b58ca14bf6c111c4b7b4fb3336b))
* **WebAPI:** Fixed the DownloadKey mapper ([8581325](https://github.com/Reaparr/Reaparr/commit/8581325aac9948b85756a3b6013ea28b9a930721))
* **WebAPI:** Fixed the DownloadTasks not being ordered by CreatedAt ([91e0b8b](https://github.com/Reaparr/Reaparr/commit/91e0b8bcbc231a90770fc64b31be5e9be14c182f))
* **WebAPI:** Fixed the DownloadWorkerTasks not registered correctly under DownloadTasks ([b1cc46b](https://github.com/Reaparr/Reaparr/commit/b1cc46b7c05ecc73d970baccee5bb68d40039007))
* **Web-UI:** Fixed the file size not taking localization into account ([05bdd5a](https://github.com/Reaparr/Reaparr/commit/05bdd5a738727485bb674c4061a48ce839375bd1))
* **WebAPI:** Fixed the FullThumbUrl to be a valid link ([97b7269](https://github.com/Reaparr/Reaparr/commit/97b7269b8a9afcb2a0610dfec0cfdac2b344563a))
* **WebAPI:** Fixed the FullThumbUrl to be a valid link ([1c0ae22](https://github.com/Reaparr/Reaparr/commit/1c0ae22d2f72a9467fd7d951b019742abfbf55d1))
* **Web-UI:** Fixed the generated plexAccount test data not being returned ([dfa6bac](https://github.com/Reaparr/Reaparr/commit/dfa6bac03c4b427dc24c7254757b2ef4f7787fb4))
* **WebAPI:** Fixed the incorrect JobStatus being sent to the front-end when creating an PlexAccount ([b48fa60](https://github.com/Reaparr/Reaparr/commit/b48fa6049ff49b0da1e8e755c394d0e77d25816f))
* **Web-UI:** Fixed the incorrect server name being displayed in the check server connection dialog ([e9fa27e](https://github.com/Reaparr/Reaparr/commit/e9fa27eb22e1d7d6dbd19728b7057fcb1835f335))
* **Web-UI:** Fixed the load transition when the page hydration happens to ensure we don't give users seizure attacks ([500d0eb](https://github.com/Reaparr/Reaparr/commit/500d0eb199c5e78f3b3b4c0fc160079d78780d7a))
* **Web-UI:** Fixed the media image in detail view not requesting the image directly from the PlexServer ([b5b9ca0](https://github.com/Reaparr/Reaparr/commit/b5b9ca089b73f6494d724e94e53a32d386356925))
* **WebAPI:** Fixed the missing ThumbUrls from tvshows when requested ([145359f](https://github.com/Reaparr/Reaparr/commit/145359f6ec233ba7901af90abe983f1364e5782d))
* **WebAPI:** Fixed the missing types in SignalR swagger generation ([2d91d7e](https://github.com/Reaparr/Reaparr/commit/2d91d7e635403b78ad7d3665771a381c075a2cfb))
* **WebAPI:** Fixed the parent download ETA not displaying ([1692796](https://github.com/Reaparr/Reaparr/commit/16927962d6dcfd03931d1ac4071f62ca298c07df))
* **Web-UI:** Fixed the percentage in the download progress bar not being rounded to 2 decimals ([2fa6591](https://github.com/Reaparr/Reaparr/commit/2fa65916ce917c4859a098bb562d653ef19ea94e))
* **WebAPI:** Fixed the PlexLibraries media not loading in the front-end ([d079843](https://github.com/Reaparr/Reaparr/commit/d079843f0d35346ed72a25d596ecc72c657532e4))
* **Web-UI:** Fixed the preferred server connection selection not being visible in the plex server settings ([dcb325a](https://github.com/Reaparr/Reaparr/commit/dcb325a012e197cd430b378ef0a1ca9597626e24))
* **WebAPI:** Fixed the refreshing of tvshow media libraries not storing the data correctly on update ([eb45e33](https://github.com/Reaparr/Reaparr/commit/eb45e33838dd44fd3f5a892d5960c977032f1e9c))
* **Web-UI:** Fixed the scrollbar always appearing on QPage component ([fe969a1](https://github.com/Reaparr/Reaparr/commit/fe969a11cd0b972e54ee02ec42152ff8048e9a50))
* **Web-UI:** Fixed the scrolling issue where the page content went underneath the top bar of the page ([34a704d](https://github.com/Reaparr/Reaparr/commit/34a704dcfa810b04c7ad6ddfbc8fba622c70ae25))
* **Web-UI:** Fixed the server and library name not being masked in the media overview bar ([8853a65](https://github.com/Reaparr/Reaparr/commit/8853a65255324aa29e9fcf1c6ccbac7677d5a8f2))
* **Web-UI:** Fixed the server connections check not showing up when a PlexAccount is setup. ([4443e0b](https://github.com/Reaparr/Reaparr/commit/4443e0b63b6807209eb39abc0e29859007b9601a))
* **WebAPI:** Fixed the server status check to be 5 seconds instead of 10 seconds ([c7578ea](https://github.com/Reaparr/Reaparr/commit/c7578ea2becdfad65d876424c00cb86cf176672e))
* **WebAPI:** Fixed the usage of DownloadTaskKey when doing a FileTask query ([d256364](https://github.com/Reaparr/Reaparr/commit/d256364f0ad953ecce6169a2730dd37c02821268))
* **Web-UI:** Fixed the vertical outline of the server status icon ([c03654c](https://github.com/Reaparr/Reaparr/commit/c03654c3b9cc272f9a2755c7e1946c042f31cbe3))
* **Web-UI:** Fixed typescript errors ([8e8bf9f](https://github.com/Reaparr/Reaparr/commit/8e8bf9f0ae7d08feb13f04413a4211e30def8a9a))
* **Web-UI:** Fixed typescript errors ([fb7e884](https://github.com/Reaparr/Reaparr/commit/fb7e8848848e5a1d1329509e80264fe9fe57beb3))
* **Web-UI:** Fixed typescript errors ([6974205](https://github.com/Reaparr/Reaparr/commit/6974205eceb4484e3520c4426ed271665447531a))
* **WebAPI:** Fixed unit tests ([a83d245](https://github.com/Reaparr/Reaparr/commit/a83d245e186395a853369591249af3888f021a0b))
* **Web-UI:** Fixed Vue warning about missing prop in FolderPathsOverview.vue ([87e6d50](https://github.com/Reaparr/Reaparr/commit/87e6d50417e815bc22a8f38f95c09c87e702d124))
* **WebAPI:** Improved the unique number generator such that it is now guaranteed to generate an unique number ([1223d05](https://github.com/Reaparr/Reaparr/commit/1223d05a4800e31454a451aebeee30d238848d4d))
* **Cypress:** Increased the timeout of the page load wait due to the new way Reaparr loads by sending the page earlier, which triggers the countdown earlier ([47efdc9](https://github.com/Reaparr/Reaparr/commit/47efdc98f1702eb3d66641e08996e1e1016437c4))
* **WebAPI:** Reimplemented the restart of a DownloadTask ([1e91e11](https://github.com/Reaparr/Reaparr/commit/1e91e118ebe861021f5b0d94c6454bde6df54311))
* **Web-UI:** Removed IMediaOverviewBus as it caused building errors and it wan't used anymore ([0077edc](https://github.com/Reaparr/Reaparr/commit/0077edc23d5713b0a1051e7a78ffd3e90676d0e6))
* **Web-UI:** Removed unused InspectPlexServerByPlexAccountIdJob ([baa3bd1](https://github.com/Reaparr/Reaparr/commit/baa3bd1cba70ee16a394eaacab5368f72818f9db))
* **WebAPI:** Reverted adding required to props as the PlexAPI is not accurate yet ([5f0991b](https://github.com/Reaparr/Reaparr/commit/5f0991b4c4d8433ea36f536d8bb09eb9b8f50148))
* **Web-UI:** Type error fix ([39b5878](https://github.com/Reaparr/Reaparr/commit/39b587802b625345d7624b18e4f06a3249cd35a0))


### Features

* **WebAPI:** Add disable background animation option in the Reaparr settings ([a7a42d9](https://github.com/Reaparr/Reaparr/commit/a7a42d9bb8bddbc975b8c706d77f723f1bcc3603))
* **Web-UI:** Added a connection status icon next to the server name on the downloads page ([6531dc3](https://github.com/Reaparr/Reaparr/commit/6531dc366f2ce6f485a130294c8382da58b16fa0))
* **Web-UI:** Added a edit icon to te editable folder path display name to make it clear this name can be changed ([5ac7508](https://github.com/Reaparr/Reaparr/commit/5ac7508198680f43454f826fdb8929dc59d6d7ac))
* **Web-UI:** Added a scratchpad page for testing components under the debug navigation menu ([b9d8877](https://github.com/Reaparr/Reaparr/commit/b9d8877eb92a9498ec6f7526687e33008b10d793))
* **Web-UI:** Added a select all button to the downloads table for every Plex server ([5fc8711](https://github.com/Reaparr/Reaparr/commit/5fc8711cf4bd2f1ebc94b1c99d4779ab10bb184c))
* **Web-UI:** Added a tooltip to the connection status of servers to indicate what it means ([f1c3189](https://github.com/Reaparr/Reaparr/commit/f1c31891ac2ba42aa06b523874989a9b7f78a463))
* **Web-UI:** Added an option to disable the animated background effect in the UI settings ([b74d3d8](https://github.com/Reaparr/Reaparr/commit/b74d3d88183cf22be44e4a1848fb0c460a70af3c))
* **Web-UI:** Added an toggle option to the app bar to disable the background ([085c241](https://github.com/Reaparr/Reaparr/commit/085c2419dc541ae3592787d9d1473a404452f636))
* **Web-UI:** Added debug options to mask server and library names, useful when taking screenshots ([0e424b0](https://github.com/Reaparr/Reaparr/commit/0e424b076552c0f4b6a5c91156b40dffc5788c96))
* **WebAPI:** Added flag whether a drive/directory has read or write permission when using the directory browser ([e3f0c8e](https://github.com/Reaparr/Reaparr/commit/e3f0c8eaaa45c2538bf93a15d4daa1604e359645))
* **Cypress:** Added way to generate DownloadTaskDTO test data for Cypress E2E tests ([204a4dc](https://github.com/Reaparr/Reaparr/commit/204a4dc3c4b1e63d355323b9bf2e9bd2acc64835))
* **WebAPI:** Allow server alias to be stored in the user settings and clean-up unused methods ([f4c0ffe](https://github.com/Reaparr/Reaparr/commit/f4c0ffee380628827f65686d3a95dac437d8a2e8))
* **Web-UI:** Allowed the server name to be editable in the server dialog ([344660c](https://github.com/Reaparr/Reaparr/commit/344660cacd57d613d1e8d76b52ca740a1fd153ce))
* **Web-UI:** Created a new user interface for the Downloads page which should be more performant ([64da99f](https://github.com/Reaparr/Reaparr/commit/64da99fa470f5ae6449e1b50c9d99797dc8b5d06))
* **Web-UI:** Display when a directory is not readable of writable in the DirectoryBrowser ([7d845d3](https://github.com/Reaparr/Reaparr/commit/7d845d34bcad8c5f6d121e00a3d1c532aae92354))
* **Web-UI:** Increases font size and color of every tooltip to increase readability ([3415568](https://github.com/Reaparr/Reaparr/commit/34155681bf385bf5092e51d50754de63e8d3e2ab))
* **WebAPI:** Migrating the startup process to the new webapplicationbuilder ([b7acf96](https://github.com/Reaparr/Reaparr/commit/b7acf964b8e72ac405a84e58ea77034ce1a6e2f7))
* **WebAPI:** Setup FastEndpoints and added test endpoint ([8a3d1d5](https://github.com/Reaparr/Reaparr/commit/8a3d1d593df32ef23b2e4defbc9667ddb381000d))
* **WebAPI:** The FolderPaths can now be changed for DownloadTasks that have not yet been started ([8c7bc13](https://github.com/Reaparr/Reaparr/commit/8c7bc13dea7e48baeb7a41741a8835a848bb99a1))
* **Web-UI:** The Print debug component now has an always expanded option ([fc41634](https://github.com/Reaparr/Reaparr/commit/fc416349d5b0018f0ecc0233bbfbd669b6024dd8))
* **Web-UI:** Toggeling the expansion of the download rows in the download table can now be done by clicking the row ([d058b59](https://github.com/Reaparr/Reaparr/commit/d058b59a05e7dac2e75ca2af75657ffb878c439b))


### Performance Improvements

* **Web-UI:** Added no transition option to improve performance for the QTreeViewTable ([ae32949](https://github.com/Reaparr/Reaparr/commit/ae32949cb52628a1752f4d505b9dc44c80bb2028))
* **Web-UI:** Enabled pagination for the Downloads which should significantly improve performance when downloading a huge number of media ([5ccde7c](https://github.com/Reaparr/Reaparr/commit/5ccde7c0a5e4e10652675668ac6fc2504265a546))
* **Web-UI:** Reaparr should load faster now by preloading the background on initial page load ([1d57be0](https://github.com/Reaparr/Reaparr/commit/1d57be062e884614d4853565528935e4c429c284))


### Reverts

* **Web-UI:** Revert mainAPI.ts ([3414be6](https://github.com/Reaparr/Reaparr/commit/3414be61833f5bafb6be81facde9897740b28be4))
* **WebAPI:** Revert OpenAPIPlex integration ([e50993a](https://github.com/Reaparr/Reaparr/commit/e50993ae2ef98556c82464fe0a72784a6fd4be31))

# [0.14.0](https://github.com/Reaparr/Reaparr/compare/v0.13.0...v0.14.0) (2023-06-22)


### Bug Fixes

* **Translations:** Added translations for the media selection dialog ([8f0c808](https://github.com/Reaparr/Reaparr/commit/8f0c8080038669365c3753ab384000687adbb6a6))
* **Translations:** Added translations for the Plex Account selector in the top right corner ([3aca494](https://github.com/Reaparr/Reaparr/commit/3aca49467c527df938ee8b9a627e944d59e9a151))
* **Web-UI:** Ensured the media selection range cannot exceed the other value, prevents min > max and max < min ([0776baf](https://github.com/Reaparr/Reaparr/commit/0776bafe00c049f761d9cdf80405f226073b98b0))
* **Web-UI:** Fixed empty help dialog appearing for options that have no text available ([8aeb3ef](https://github.com/Reaparr/Reaparr/commit/8aeb3efff460867b2b8035d261ed6db4fcb6821b))
* **Web-UI:** Fixed missing translations for the account selector in the top right corner ([943f12f](https://github.com/Reaparr/Reaparr/commit/943f12ff6444c7024302dbff5af6a10c9e3f61e1))
* **Web-UI:** Fixed the download commands not working when download confirmations have been disabled ([8cbd736](https://github.com/Reaparr/Reaparr/commit/8cbd736253358b4b29a9f3cf009d9ab8caf6543e))
* **Web-UI:** Fixed the DownloadSpeedLimit not displaying the correct value when changing in the server dialog ([e4a059f](https://github.com/Reaparr/Reaparr/commit/e4a059f02a40ede3ed42fc5f58763e67398fa080))
* **Web-UI:** Fixed the index shown in the MediaTable not being the same as the one coming from the back-end ([2100fef](https://github.com/Reaparr/Reaparr/commit/2100fef77d605d002c59dc7f69c9d4404a6eacff))
* **WebAPI:** Fixed the media data not being properly sorted, this will now use Natural sort by using the title instead of the sortTitle ([603d1fe](https://github.com/Reaparr/Reaparr/commit/603d1fe209283ab27161c56cbcb30432caecb602))
* **Web-UI:** Fixed the media table sort requiring two clicks to begin sorting ([e024617](https://github.com/Reaparr/Reaparr/commit/e024617babb4c47f3cee625fe082e2622365c05c))
* **Web-UI:** Fixed the MediaTable sorting not correctly using the same sorting as the back-end ([27613f1](https://github.com/Reaparr/Reaparr/commit/27613f1fdc055b744595843767aa791ae5806eaa))
* **WebAPI:** Fixed the PlexServerConnections connection status not being retrieved when requesting the PlexServers ([53b236e](https://github.com/Reaparr/Reaparr/commit/53b236ecd6b6f97634bc352ca2151d71b7b7f46d))
* **Web-UI:** Fixed the PlexStatus icon displayed next to the server names not being updated when it changes ([29b4b49](https://github.com/Reaparr/Reaparr/commit/29b4b49f75725937a7a4f268f4f1153457eb2afa))
* **Web-UI:** Fixed the Poster / Table dropdown selector not working due to Pinia migration ([c308986](https://github.com/Reaparr/Reaparr/commit/c308986e629493ccf0c2c7b8c84d84ea747eea1d))
* **Web-UI:** Fixed the service layer initializing too early when for example Pinia is not ready yet, this is now dependent on a Nuxt hook ([289de1d](https://github.com/Reaparr/Reaparr/commit/289de1de57c92c48efb4eee1c385aca191c41d68))
* **Web-UI:** Fixed the settings being updated on page load when nothing could have changed ([7c44fd5](https://github.com/Reaparr/Reaparr/commit/7c44fd5d9d3b0abc68751de6877bf9b59cb30df6))


### Features

* **Web-UI:** Added a selection button to the MediaTable to make it way easier to make media selections ([0323cbf](https://github.com/Reaparr/Reaparr/commit/0323cbf289f91277bc77fcd55f147bf845aa35a2))


### Performance Improvements

* **Web-UI:** Replaced the settings system with Pinia instead of an observable store, this should improve performance ([a628731](https://github.com/Reaparr/Reaparr/commit/a62873196bd51b019ef2ee3310c02ec0f699b30c))
* **Web-UI:** Settings are now updated once every second when something changes, this debounce effect prevents DDOS'ing the back-end ([a8116d3](https://github.com/Reaparr/Reaparr/commit/a8116d3ee533c41167c55f53efe298ca78965d32))

# [0.14.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.13.0...v0.14.0-dev.1) (2023-06-22)


### Bug Fixes

* **Translations:** Added translations for the media selection dialog ([8f0c808](https://github.com/Reaparr/Reaparr/commit/8f0c8080038669365c3753ab384000687adbb6a6))
* **Translations:** Added translations for the Plex Account selector in the top right corner ([3aca494](https://github.com/Reaparr/Reaparr/commit/3aca49467c527df938ee8b9a627e944d59e9a151))
* **Web-UI:** Ensured the media selection range cannot exceed the other value, prevents min > max and max < min ([0776baf](https://github.com/Reaparr/Reaparr/commit/0776bafe00c049f761d9cdf80405f226073b98b0))
* **Web-UI:** Fixed empty help dialog appearing for options that have no text available ([8aeb3ef](https://github.com/Reaparr/Reaparr/commit/8aeb3efff460867b2b8035d261ed6db4fcb6821b))
* **Web-UI:** Fixed missing translations for the account selector in the top right corner ([943f12f](https://github.com/Reaparr/Reaparr/commit/943f12ff6444c7024302dbff5af6a10c9e3f61e1))
* **Web-UI:** Fixed the download commands not working when download confirmations have been disabled ([8cbd736](https://github.com/Reaparr/Reaparr/commit/8cbd736253358b4b29a9f3cf009d9ab8caf6543e))
* **Web-UI:** Fixed the DownloadSpeedLimit not displaying the correct value when changing in the server dialog ([e4a059f](https://github.com/Reaparr/Reaparr/commit/e4a059f02a40ede3ed42fc5f58763e67398fa080))
* **Web-UI:** Fixed the index shown in the MediaTable not being the same as the one coming from the back-end ([2100fef](https://github.com/Reaparr/Reaparr/commit/2100fef77d605d002c59dc7f69c9d4404a6eacff))
* **WebAPI:** Fixed the media data not being properly sorted, this will now use Natural sort by using the title instead of the sortTitle ([603d1fe](https://github.com/Reaparr/Reaparr/commit/603d1fe209283ab27161c56cbcb30432caecb602))
* **Web-UI:** Fixed the media table sort requiring two clicks to begin sorting ([e024617](https://github.com/Reaparr/Reaparr/commit/e024617babb4c47f3cee625fe082e2622365c05c))
* **Web-UI:** Fixed the MediaTable sorting not correctly using the same sorting as the back-end ([27613f1](https://github.com/Reaparr/Reaparr/commit/27613f1fdc055b744595843767aa791ae5806eaa))
* **WebAPI:** Fixed the PlexServerConnections connection status not being retrieved when requesting the PlexServers ([53b236e](https://github.com/Reaparr/Reaparr/commit/53b236ecd6b6f97634bc352ca2151d71b7b7f46d))
* **Web-UI:** Fixed the PlexStatus icon displayed next to the server names not being updated when it changes ([29b4b49](https://github.com/Reaparr/Reaparr/commit/29b4b49f75725937a7a4f268f4f1153457eb2afa))
* **Web-UI:** Fixed the Poster / Table dropdown selector not working due to Pinia migration ([c308986](https://github.com/Reaparr/Reaparr/commit/c308986e629493ccf0c2c7b8c84d84ea747eea1d))
* **Web-UI:** Fixed the service layer initializing too early when for example Pinia is not ready yet, this is now dependent on a Nuxt hook ([289de1d](https://github.com/Reaparr/Reaparr/commit/289de1de57c92c48efb4eee1c385aca191c41d68))
* **Web-UI:** Fixed the settings being updated on page load when nothing could have changed ([7c44fd5](https://github.com/Reaparr/Reaparr/commit/7c44fd5d9d3b0abc68751de6877bf9b59cb30df6))


### Features

* **Web-UI:** Added a selection button to the MediaTable to make it way easier to make media selections ([0323cbf](https://github.com/Reaparr/Reaparr/commit/0323cbf289f91277bc77fcd55f147bf845aa35a2))


### Performance Improvements

* **Web-UI:** Replaced the settings system with Pinia instead of an observable store, this should improve performance ([a628731](https://github.com/Reaparr/Reaparr/commit/a62873196bd51b019ef2ee3310c02ec0f699b30c))
* **Web-UI:** Settings are now updated once every second when something changes, this debounce effect prevents DDOS'ing the back-end ([a8116d3](https://github.com/Reaparr/Reaparr/commit/a8116d3ee533c41167c55f53efe298ca78965d32))

# [0.13.0](https://github.com/Reaparr/Reaparr/compare/v0.12.0...v0.13.0) (2023-06-13)


### Bug Fixes

* **WebAPI:** Fixed Plex server HTTPS connections not working due to missing user-agent ([8f9a48a](https://github.com/Reaparr/Reaparr/commit/8f9a48a1376a09e2ba897cc4847d85e900bfe3af))
* **GithubActions:** Fixed the caching of back-end nuget packages by specificing the package.lock.json path ([d5e67b3](https://github.com/Reaparr/Reaparr/commit/d5e67b3d91a8fc4af173dfc23bc3b91d8e932373))
* **WebAPI:** Fixed the CreatedAt and LastSeen values being incorrect when displaying the Server Dialog ([4c0bd15](https://github.com/Reaparr/Reaparr/commit/4c0bd159a608815d3f39bce05bcaaa3224a3d07a))
* **WebAPI:** Fixed the download confirmation dialog displaying a checkbox which is not needed ([8c894cb](https://github.com/Reaparr/Reaparr/commit/8c894cb9cb9a0da869e529b73303d7360c889ce2))
* **WebAPI:** Fixed the downloading failing when connecting with an HTTPS connection ([2ed6c48](https://github.com/Reaparr/Reaparr/commit/2ed6c48b817d68d61d3601265144a56b0e14a586))
* **Web-UI:** Fixed the empty Server Dialog -> Download Destinations page when no libraries are available by displaying an error text ([c527d6b](https://github.com/Reaparr/Reaparr/commit/c527d6bc5d8fee9987f19fda1b6adc7ae7d5afa9))
* **WebAPI:** Fixed the logging being formatted in a bloated redundant way, it should now be more compact ([9bc1b8d](https://github.com/Reaparr/Reaparr/commit/9bc1b8d651337f70addbd57a1625cf04d323779e))
* **WebAPI:** Fixed the UrlMaskingOperator being matched on everything ([e5a5579](https://github.com/Reaparr/Reaparr/commit/e5a5579b3b44af213045954000b6a28ea5194b32))
* **Web-UI:** Fixed the width being too large of the download details dialog ([3d87b7a](https://github.com/Reaparr/Reaparr/commit/3d87b7a7d8c8d7ff4d51b5742e5d30549ff8db01))
* **GithubActions:** Specified the solution to build ([0f62a44](https://github.com/Reaparr/Reaparr/commit/0f62a44ccd9ef4a53ff7a205e9a1b84c5ffb839f))
* **GithubActions:** Specified the solution to restore ([43a188b](https://github.com/Reaparr/Reaparr/commit/43a188b9f4b1584eea62a0a228e6aa95603ae2b1))


### Features

* **WebAPI:** All sensitive data that is logged is now masked, which should make sharing the logs much safer. ([de96ee3](https://github.com/Reaparr/Reaparr/commit/de96ee3d7036325e5c15085d98b29cc2d3e57dec))
* **Web-UI:** Improved the layout of the server data displayed in the Server settings dialog ([b456ed0](https://github.com/Reaparr/Reaparr/commit/b456ed06cf530de69fc19d1a3e85de25a4478806))


### Performance Improvements

* **Docker:** Added --no-restore to the build step and used --locked-mode to force the use of the NuGet package.lock.json ([fb1a63f](https://github.com/Reaparr/Reaparr/commit/fb1a63f80754a0665302f949936e4e0aa0269a4c))
* **GithubActions:** Possible optimization to share the build step for the back-end testing jobs ([6e38832](https://github.com/Reaparr/Reaparr/commit/6e38832a228e4516808a16082cb55fc0e5354e94))


### Reverts

* **GithubActions:** Revert attempt to split the build step for back-end testing ([77e4ec8](https://github.com/Reaparr/Reaparr/commit/77e4ec874e1745aa49adf1c8c303ba703acb97b4))

# [0.13.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.12.0...v0.13.0-dev.1) (2023-06-13)


### Bug Fixes

* **WebAPI:** Fixed Plex server HTTPS connections not working due to missing user-agent ([8f9a48a](https://github.com/Reaparr/Reaparr/commit/8f9a48a1376a09e2ba897cc4847d85e900bfe3af))
* **GithubActions:** Fixed the caching of back-end nuget packages by specificing the package.lock.json path ([d5e67b3](https://github.com/Reaparr/Reaparr/commit/d5e67b3d91a8fc4af173dfc23bc3b91d8e932373))
* **WebAPI:** Fixed the CreatedAt and LastSeen values being incorrect when displaying the Server Dialog ([4c0bd15](https://github.com/Reaparr/Reaparr/commit/4c0bd159a608815d3f39bce05bcaaa3224a3d07a))
* **WebAPI:** Fixed the download confirmation dialog displaying a checkbox which is not needed ([8c894cb](https://github.com/Reaparr/Reaparr/commit/8c894cb9cb9a0da869e529b73303d7360c889ce2))
* **WebAPI:** Fixed the downloading failing when connecting with an HTTPS connection ([2ed6c48](https://github.com/Reaparr/Reaparr/commit/2ed6c48b817d68d61d3601265144a56b0e14a586))
* **Web-UI:** Fixed the empty Server Dialog -> Download Destinations page when no libraries are available by displaying an error text ([c527d6b](https://github.com/Reaparr/Reaparr/commit/c527d6bc5d8fee9987f19fda1b6adc7ae7d5afa9))
* **WebAPI:** Fixed the logging being formatted in a bloated redundant way, it should now be more compact ([9bc1b8d](https://github.com/Reaparr/Reaparr/commit/9bc1b8d651337f70addbd57a1625cf04d323779e))
* **WebAPI:** Fixed the UrlMaskingOperator being matched on everything ([e5a5579](https://github.com/Reaparr/Reaparr/commit/e5a5579b3b44af213045954000b6a28ea5194b32))
* **Web-UI:** Fixed the width being too large of the download details dialog ([3d87b7a](https://github.com/Reaparr/Reaparr/commit/3d87b7a7d8c8d7ff4d51b5742e5d30549ff8db01))
* **GithubActions:** Specified the solution to build ([0f62a44](https://github.com/Reaparr/Reaparr/commit/0f62a44ccd9ef4a53ff7a205e9a1b84c5ffb839f))
* **GithubActions:** Specified the solution to restore ([43a188b](https://github.com/Reaparr/Reaparr/commit/43a188b9f4b1584eea62a0a228e6aa95603ae2b1))


### Features

* **WebAPI:** All sensitive data that is logged is now masked, which should make sharing the logs much safer. ([de96ee3](https://github.com/Reaparr/Reaparr/commit/de96ee3d7036325e5c15085d98b29cc2d3e57dec))
* **Web-UI:** Improved the layout of the server data displayed in the Server settings dialog ([b456ed0](https://github.com/Reaparr/Reaparr/commit/b456ed06cf530de69fc19d1a3e85de25a4478806))


### Performance Improvements

* **Docker:** Added --no-restore to the build step and used --locked-mode to force the use of the NuGet package.lock.json ([fb1a63f](https://github.com/Reaparr/Reaparr/commit/fb1a63f80754a0665302f949936e4e0aa0269a4c))
* **GithubActions:** Possible optimization to share the build step for the back-end testing jobs ([6e38832](https://github.com/Reaparr/Reaparr/commit/6e38832a228e4516808a16082cb55fc0e5354e94))


### Reverts

* **GithubActions:** Revert attempt to split the build step for back-end testing ([77e4ec8](https://github.com/Reaparr/Reaparr/commit/77e4ec874e1745aa49adf1c8c303ba703acb97b4))

# [0.12.0](https://github.com/Reaparr/Reaparr/compare/v0.11.1...v0.12.0) (2023-05-29)


### Bug Fixes

* **Database:** Fixed a null exception when a library with no media is returned ([970473e](https://github.com/Reaparr/Reaparr/commit/970473e490687c4cc7b9b3fb35716fb63d8b3e8f))
* **Web-UI:** Fixed the single column media posters jumping to the correct size when navigating libraries ([9c6cad9](https://github.com/Reaparr/Reaparr/commit/9c6cad9fb0a14328e7d5e0bd128849110624b57d))
* **Web-UI:** Fixed the verification dialog not appearing when 2FA is enabled ([8150978](https://github.com/Reaparr/Reaparr/commit/815097817684ab7141b8657ce85d9c03331d15b6))


### Features

* **Web-UI:** A home icon is displayed next to the server list when that server is owned by a Plex account ([fe8ed71](https://github.com/Reaparr/Reaparr/commit/fe8ed71bcdab45f26323973fd33b37c49ea76726))
* **Web-UI:** The Plex servers on the left side are now ordered from owned to then sorted by name ([dec9bb0](https://github.com/Reaparr/Reaparr/commit/dec9bb0646e3ded70c03b6f96e1d2a2b95f8f005))

# [0.12.0-dev.1](https://github.com/Reaparr/Reaparr/compare/v0.11.1...v0.12.0-dev.1) (2023-05-29)


### Bug Fixes

* **Database:** Fixed a null exception when a library with no media is returned ([970473e](https://github.com/Reaparr/Reaparr/commit/970473e490687c4cc7b9b3fb35716fb63d8b3e8f))
* **Web-UI:** Fixed the single column media posters jumping to the correct size when navigating libraries ([9c6cad9](https://github.com/Reaparr/Reaparr/commit/9c6cad9fb0a14328e7d5e0bd128849110624b57d))
* **Web-UI:** Fixed the verification dialog not appearing when 2FA is enabled ([8150978](https://github.com/Reaparr/Reaparr/commit/815097817684ab7141b8657ce85d9c03331d15b6))


### Features

* **Web-UI:** A home icon is displayed next to the server list when that server is owned by a Plex account ([fe8ed71](https://github.com/Reaparr/Reaparr/commit/fe8ed71bcdab45f26323973fd33b37c49ea76726))
* **Web-UI:** The Plex servers on the left side are now ordered from owned to then sorted by name ([dec9bb0](https://github.com/Reaparr/Reaparr/commit/dec9bb0646e3ded70c03b6f96e1d2a2b95f8f005))

## [0.11.1](https://github.com/Reaparr/Reaparr/compare/v0.11.0...v0.11.1) (2023-05-28)


### Bug Fixes

* **Web-UI:** Fixed the boot loop logo due to wrong url used when opening the web ui ([768e08b](https://github.com/Reaparr/Reaparr/commit/768e08bab1867c1b8b514e9dfd0242852c32b533))

# [0.11.0](https://github.com/Reaparr/Reaparr/compare/v0.10.0...v0.11.0) (2023-05-27)


### Bug Fixes

* **Web-UI:** Cleaned up and fixed the 2FA process of creating a PlexAccount ([0332b8e](https://github.com/Reaparr/Reaparr/commit/0332b8e92b6239eabbd6d60095330a65c4f2f011))
* **Web-UI:** CSS .glass-background now changes color based on dark/light mode ([29f0c1f](https://github.com/Reaparr/Reaparr/commit/29f0c1f563e3ac6e6c98c4dd449855697c89efc7))
* **Web-UI:** Display the default image when failing to request a thumbnail in the PosterView of media ([5edad67](https://github.com/Reaparr/Reaparr/commit/5edad67a3bcd96c04201b31d9f1e34f4e10a74da))
* **Web-UI:** Ensured the plugins are only running on client when SSR: true ([23939a6](https://github.com/Reaparr/Reaparr/commit/23939a693d54bf0b926168b98258b1371b226ff6))
* **Web-UI:** Fixed alignment of the setup page tab contents ([ef51256](https://github.com/Reaparr/Reaparr/commit/ef51256f2bb109186c4ae2f43fa4f1bf7ef0e3f5))
* **Web-UI:** Fixed the AccountDialog not closing after an account has been deleted ([6411108](https://github.com/Reaparr/Reaparr/commit/6411108aa1434d04494f85ade68f5f0d2208d2de))
* **Web-UI:** Fixed the Alphabet navigation for the Poster view of the media collection ([13ca76c](https://github.com/Reaparr/Reaparr/commit/13ca76c72c633fe6d7b303479468c29d23112dec))
* **Web-UI:** Fixed the BaseButton not allowing the passing in of the default slot content ([4164634](https://github.com/Reaparr/Reaparr/commit/4164634f9184539d81387e181d0fc5bbf3c433db))
* **Web-UI:** Fixed the confirmation dialog not showing wen pressing the Reset Database button under settings => advanced ([1dcf2df](https://github.com/Reaparr/Reaparr/commit/1dcf2df66e766ede25ea52032d46de28fa2cd5fd))
* **Web-UI:** Fixed the connections not being displayed when checking the server connections ([232686a](https://github.com/Reaparr/Reaparr/commit/232686a572e3de7889789f0f5fa80a66cf6ab6b8))
* **Web-UI:** Fixed the DetailsOverview navigating and loading correctly ([9b6c13c](https://github.com/Reaparr/Reaparr/commit/9b6c13c25cf5494d2e5fe78f5d63bee3c1eedc65))
* **Web-UI:** Fixed the display of refreshing the library data ([1a4a56c](https://github.com/Reaparr/Reaparr/commit/1a4a56c8d9d676d846a474166276a986b70cdcbc))
* **Web-UI:** Fixed the download table action buttons wraping due to not enough space ([0f25689](https://github.com/Reaparr/Reaparr/commit/0f256896812de181f205087a277bf8159d9c2155))
* **Web-UI:** Fixed the highlight causing big margins around the elements that it's applies to ([af044a6](https://github.com/Reaparr/Reaparr/commit/af044a6344984a8592864257ed149bc4e823ad48))
* **Web-UI:** Fixed the hover and click not working on TVshow links to display the details page ([34eadc1](https://github.com/Reaparr/Reaparr/commit/34eadc13ec052a6c7f38d01b6f61a4f6acad922d))
* **Web-UI:** Fixed the inconsistent heights in relation to the content of the Dialogs based on QCardDialog ([95cc50b](https://github.com/Reaparr/Reaparr/commit/95cc50b9ee4e4a49f201b2665d16c8632567de23))
* **Web-UI:** Fixed the media details not displaying and the opening/closing of the DetailsOverview is now handled by eventbus. ([70a1535](https://github.com/Reaparr/Reaparr/commit/70a15359a050df61062ffc794eab2507367e4836))
* **Web-UI:** Fixed the MediaDetailsDialog css being applied to all dialog components ([9d565a9](https://github.com/Reaparr/Reaparr/commit/9d565a9930a5b62ce9ff3ce1d19d8dfdebdd5e68))
* **Web-UI:** Fixed the navigating between TvShow and TvShowDetail that it maintains the scroll of the MediaOverview ([afd8ebe](https://github.com/Reaparr/Reaparr/commit/afd8ebe9f932dcf757badd1ad6f5696c3e35d5b7))
* **Web-UI:** Fixed the Reaparr logo margin in the AppBar at the top ([ea117d7](https://github.com/Reaparr/Reaparr/commit/ea117d7e962d3ae81514a0edcefb7683d5a59e67))
* **Web-UI:** Fixed the poster media view not handling huge 50.000+ media libraries ([2603092](https://github.com/Reaparr/Reaparr/commit/2603092c6322f934e9a8f3233a3bceb2053f7bf0))
* **Web-UI:** Fixed the prop persistent not being passed into q-dialog in the QCardDialog ([0fac6b0](https://github.com/Reaparr/Reaparr/commit/0fac6b0d5cd8b4f9d260e97bbfe14e9514a206c6))
* **Web-UI:** Fixed the repeated Library refresh request issue ([2defbed](https://github.com/Reaparr/Reaparr/commit/2defbedfcb1fc9b4dee7581d3edb79413535e1ca))
* **Web-UI:** Fixed the selection of rows and passing through the download commands ([50561d1](https://github.com/Reaparr/Reaparr/commit/50561d12a5bcadd8b7b491969e89c9383b7b08bf))
* **Web-UI:** Fixed the wrong color value passed in GoToButton on the home page ([7f74ea4](https://github.com/Reaparr/Reaparr/commit/7f74ea48a26dbf43cf57423c17dc8fd827d78cfd))
* **Web-UI:** Hidden the Database section under settings as the "Reset Database" button is not working in the short term ([cb39d06](https://github.com/Reaparr/Reaparr/commit/cb39d061e6081fb14616725dd92886936ab8f968))
* **Web-UI:** Improved the vertical letter navigation on the media overview page to align and stretch properly while making it a bit bigger ([7d59121](https://github.com/Reaparr/Reaparr/commit/7d59121fc97fb5806b6518a84bff5b24efc2a51b))
* **Web-UI:** include input,select and textarea when determining the text-color ([74d1da9](https://github.com/Reaparr/Reaparr/commit/74d1da99ebd4e5b694f3ece21667e4e34fbe4da5))
* **Web-UI:** Mock data now respects the seed setting and will auto-increment when multiple elements are generated ([c6ed02a](https://github.com/Reaparr/Reaparr/commit/c6ed02a2f1ab16692d7a95a4309f5113e54b1cff))
* **Web-UI:** On completion of checking all server connections in the dialog, the treeview will now collapse ([de3681c](https://github.com/Reaparr/Reaparr/commit/de3681cb27159e0200674a47f28ac730ecc33631))
* **Web-UI:** Set a max-width for the confirmation dialog to not take up 100% screen width ([d354bb6](https://github.com/Reaparr/Reaparr/commit/d354bb60c5b74a9b5fcdcde195c427a54eab73e4))
* **WebAPI:** The media quality formats are now added to the PlexMediaSlim object ([3b15052](https://github.com/Reaparr/Reaparr/commit/3b15052837701da225beba35bf2880b2bd9a299a))


### Features

* **WebAPI:** Added a "debugMode" settings property that will show/hide debugging functionality ([5b90812](https://github.com/Reaparr/Reaparr/commit/5b908121abbc44cbae9fff276994232c8489309f))
* **Web-UI:** Added a highlight animation when navigating with the Alphabet navigation on the media page ([cb7a27d](https://github.com/Reaparr/Reaparr/commit/cb7a27d8afd169072f68347dee14abd1494b5908))
* **Web-UI:** Added a highlight around the poster when navigating with the Alphabet navigation in the poster media view ([a9b4def](https://github.com/Reaparr/Reaparr/commit/a9b4def5a0d54b618ff8067a97406e99e4442e4c))
* **WebAPI:** Added a separate endpoint for getting the full TvShow media data ([9e1d7f9](https://github.com/Reaparr/Reaparr/commit/9e1d7f94f50fbd0d99342238557d3f01a021326c))
* **Web-UI:** Added debug section under advanced settings which has a toggle to enable/disable debug mode ([0b65743](https://github.com/Reaparr/Reaparr/commit/0b65743407977f19e11d6227d789cf3a479fb40e))
* **Web-UI:** Added factory to generate mock Plex media data ([2035a4c](https://github.com/Reaparr/Reaparr/commit/2035a4c6f48fbe9924b50c6a766bdca17b5b0887))
* **WebAPI:** Added index to the PlexMediaSlimDTO ([f32f093](https://github.com/Reaparr/Reaparr/commit/f32f0936f68ce336cf0c13807ceda8d17db4d246))
* **WebAPI:** Enabled XML-documentation display in the Nswag/Swagger UI ([225c453](https://github.com/Reaparr/Reaparr/commit/225c4531ae78a439be05fc51e16da269e7613355))
* **Web-UI:** The debug menu items are now only displayed when debugMode is enabled ([a8e4349](https://github.com/Reaparr/Reaparr/commit/a8e4349dbe807bf8f81c96abd0084d681eaf69e0))
* **Web-UI:** The Print component will now only be displayed when debugMode is enabled ([23654c8](https://github.com/Reaparr/Reaparr/commit/23654c85bcd857bdf4534ece39c3ad9dd66df5d6))
* **Web-UI:** The table overview when viewing the media in a library now works with virtual scrolling, meaning it can view huge libraries with ease ([b9beea4](https://github.com/Reaparr/Reaparr/commit/b9beea49c99267dd2253bea466e8e018021ceaba))


### Performance Improvements

* **Web-UI:** Calculating which letters to display in the Alphabet Navigation is now 90% faster ([45f116e](https://github.com/Reaparr/Reaparr/commit/45f116e12537a790700a21025140fe6036e4d302))
* **WebAPI:** Removed the allMedia option from the api endpoint /PlexLibrary/{id} ([736112a](https://github.com/Reaparr/Reaparr/commit/736112a351c88bce1fd0f538c1e8ae5403f53c22))
* **WebAPI:** The PlexMediaSlim now contains the thumbnail image url, this greatly speeds up the loading of the thumbnail ([3637711](https://github.com/Reaparr/Reaparr/commit/3637711cffcdb5dfe03fb4bcbcfb7fd63d2fcaa1))
* **WebAPI:** The TvShowDetail API endpoint now returns onl the data that is needs to significiantly increase performance when viewing Seasons ([4f0966e](https://github.com/Reaparr/Reaparr/commit/4f0966ed00f4c746e141b0c682e0739c2b1bd6bb))

## [0.11.0]

**Major migration of the front-end from Vue 2 / Nuxt 2 and Vuetify v2 to Vue 3 / Nuxt 3 and Quasar v2. Basically a complete rewrite of the front-end.**

### Added
- Added sorting to the library media page with the ability to sort by title, date added, date updated, and size.

### Changed
 - Major front-end migration to Vue 3 / Nuxt 3 and Quasar v2
 - Added a selection count to the library media page and media detail page.
 - Viewing a huge library media collection should be much smoother due to paginated loading and virtual scrolling.
 - Re-designed the Reaparr setup wizard to flow better and be more intuitive.
 - The estimated time shown when downloading a media item is now shown in a shorter notation, 06:45:09.

### Optimizations
 - Optimized the media viewing to be more performant and use less memory.
 - Calculating the download preview, which is shown when a pop-up opens that asks if you're sure that you want to download the selected media, is now done in the background and not on the fly.
 -

### Fixed
 - Fixed the laggyness when scrolling on the media pages when selecting a plex library.
 - Fixed the checkbox selection not working in the media pages when selecting a plex library.
 - Fixed the seasons when viewing a tvShow not being sorted correctly.
 - Fixed the logging spam "Something failed but no errors were available in the result"
 - Fixed the DownloadDetail dialog not containing the DownloadUrl when a task can be downloaded.
 - Fixed the Downloads count that is displayed in the menu to the left displaying an incorrect number, this number is now based on the amount of downloadable media entries


## [0.10.0-RC1]

This is most, not all, of the many changes that have been made in this version.

### Added
 - Cypress Front-end testing

### Changed
 - Each PlexServer now has multiple connections available which can be chosen individually, this should solve proxy and connection issues with servers.
 - Each PlexServer can now have a preferred connection that can be set in the server settings.
 - Changed the infrastructure for the download and fileMerge process to be hosted in the Quartz Background services.
 - Big re-organization of the PlexAccount setup process.
 - Upgraded to .NET 7.0 and Entity Framework 7.0, as well as many other dependencies.

### Fixed
 - Fixed the two-factor authentication not working due to the Plex error not being passed on from the http client
 - Fixed the front-end page data not refreshing when deleting an PlexAccount
 - Fixed download process not working when the PlexServer is behind a proxy
 - Fixed the timeout issue when communicating with a big PlexServer, it's currently 60 seconds before it times out.
 - Fixed "cannot access the file" exceptions which were due to resources not being disposed correctly.

### Optimizations
 - Optimized the token retrieval process for Downloads, this should make downloads more resilient to token refreshes.
 - Optimized the download progress to the front-end, this should make the download progress more accurate and less laggy.
 - Optimized all the project to a vertical slice architecture, this should make the project more maintainable and easier to extend.
 - Optimized the logging to be more consistent and easier to read.

## [0.9.1]

### Added
 - Added a setup question on the home page instead of forcefully redirecting to the setup page when the setup hasn't been yet skipped or completed
### Changed
 - Made the updating of settings in the front-end a true observable to make it chain-able and await-able to only do stuff after it has updated the settings
### Fixed
 - Fixed the setup loop that happened for some users where they got redirected back to the setup screen after finishing or skipping by removing the redirect
 - Fixed the downloadSegments setting not updating correctly when changed

## [0.9.0]

This has been a major refactoring with many unit and integration tests added to ensure stability.
This means you will have to empty the config folder before installing to ensure proper workings.

Note: This, by a long shot, doesn't encompasses all the changes and fixes that have been made in this version due to the large scope of the changes.

### Added
 - Per server configurable download speed limit (See server settings > Server Configuration)
 - Per server and library configurable folder destination where media downloaded will automatically be moved too.
 - German UI language (Thanks to [Padso4tw](https://github.com/padso4tw)!)
 - Jest and Cypress testing infrastructure
 - Added a new server command in the server settings to inspect a server connection and attempt to fix it.
 - Added a loading screen with a rotating logo

### Changed
 - Removed batch commands from the download page, these were not working and overcomplicated things too much.
 - Migrated project to .NET 6, which brings many performance improvements
 - Added a loading icon to the button when checking the server status in the server configuration
 - The server command "Re-sync Library media" now displays a loading animation.
 - Replaced every button with a more performant and consistent button construction
 - Thumbnails displayed for movies and tvShows on the library pages in poster mode are now displayed from cache when navigating around Reaparr.
 - Rewrote the code for the entire download process to be more stable and allow for future features
 - Rewrote the user settings modules (ReaparrConfig.json) which is now easy to extend and more resilient.
 - Many performance improvements!

### Fixed
 - Fixed the setup of Plex accounts in Reaparr which was failing due to a login format change on the side of Plex
 - Fixed the slow downloads by increasing the download buffer, might make it configurable based on the hardware Reaparr is run on. (Thanks to [BakasuraRCE](https://github.com/BakasuraRCE)!)
 - Fixed the opening of the server settings not defaulting back to its first tab
 - Fixed the download progress not updating after a while due to SignalR disconnects
 - Fixed the retrieval of the ServerStatus not working when a timeout happens.
 - Fixed the notifications not always being shown and updated correctly when an error happens.
 - Fixed the download confirmation window not hiding after clicking confirm #122
 - Fixed the page background effect breaking when the browser does not support WebGL. It will now display a still image of the background.

## [0.8.7]

### Added

- Added two-factor authentication compatibility, meaning Reaparr now works with two-factor authentication protected Plex accounts.
- Added the awesome French translation from @starnakin, thank you so much!
- Added a progress window showing the individual servers being connected to when setting up an PlexAccount.
- Added a warning that deleting an plexAccount might take a long time due to the amount of data which has to be deleted.
- Added a thank you for the awesome contribution from Starnakin to the README.
- Added migration check for adding ClientId to already created Plex accounts, this avoids users having to re-setup their accounts in Reaparr.

### Changed

- When confirming an action in the confirmation dialog, a loading spinner will now show.
- When a translation is missing, it will now show the English variant.
- When no downloads have been selected in the download page, the "Clear Completed" is now disabled.
- Updated to the new PlexAPI SignIn process
- Improved the PlexApi HttpClient to return errors given by Plex.

### Fixed

- Fixed the "New device connected" spam that Plex server owners would get due to a randomly generated ClientId's being used per request by Reaparr. This is now unique and consistent for every PlexAccount.
- Fixed the delete button missing from the Plex account update window.
- Fixed the confirmation window prevented from being closed.
- Fixed the menu titles now being translatable.
- Fixed the DateTime settings not being translatable.
- Fixed the browser client not updating its store after resetting the database.
- Fixed the error window having a very wide window due to unwrapped text.
- Fixed the TvShows libraries getting stuck in a infinite loading loop when viewed, TV show libraries can now be viewed again.

## [0.8.6]

### Added

- Added an improved Notification sidebar
- Added all text as translations keys to the language file to ensure everything can get translated.
- Added Language switcher under Settings => UI, only supported English and the to be translated French.

### Changed

- Notifications can now be cleared with a click of a button
- Fixed the lang folder link in the README
- Added the French language file and language support (Still needs translations).
- Added the language option to the config file
- Rewritten how settings are saved, they should now work much better
- Added feedback section to the ReadMe
- Ensured the config/settings file is now better protected against corruption and invalid values

### Fixed

- Fixed the Letter navigation in Poster view not scrolling the page.
- Fixed the setup page not shown automatically on a new install.
- Fixed the percentage in download speed having too many decimals, namely in the Downloads page.
- Fixed where empty help buttons would show up next to options.
- Fixed the checkboxes in settings not working.
- Fixed the misalignment of the checkboxes in the settings page.
- Fixed an issue where statically generating the WebUI in development would return the wrong api url.
- Fixed an error when trying to add a Movie or TvShow destination folder under Settings => Paths, should now work correctly
- Fixed the "no downloads" message not always showing correctly when there are no downloads in progress.
- Fixed the setup page not showing the correct background and throwing an error when that happens
- Fixed the skip button during the setup wizard not appearing

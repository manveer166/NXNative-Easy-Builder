# NXNative Easy Builder

**Windows Easy Installer · MacOS Builder**

Prepare the NXNative toolchain and turn your own Nintendo Switch game dumps into iOS and iPadOS IPAs or macOS apps. The Windows installer sets up NXNativeWindows and opens its IPA Builder. The Mac builder uses native NXNative for both build targets.

Click **Install Everything** to check GitHub for updates and set up the required tools. Then pick your NSP or other supported game dump in the builder.

Created by **iSolution**. Based on [**NXNative**][nxnative] by **seedds** and [**NXNativeWindows**][nxnativewindows] by **zenvyzenshiro**.

[iSolution on YouTube][isolution]
## Downloads

[![Download for Windows x64](https://img.shields.io/badge/Windows%20x64-Download-0078D4?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI%2BPHBhdGggZmlsbD0iI2ZmZiIgZD0iTTIgMmg5djlIMnpNMTMgMmg5djloLTl6TTIgMTNoOXY5SDJ6TTEzIDEzaDl2OWgtOXoiLz48L3N2Zz4%3D&logoColor=white)](https://applejr.ipadownloads.com/signed/NXNativeWindowsEasyInstaller-v2.0.2-win-x64.exe.zip)
[![Download for Windows ARM64](https://img.shields.io/badge/Windows%20ARM64-Download-6053BA?style=for-the-badge&logo=arm&logoColor=white)](https://applejr.ipadownloads.com/signed/NXNativeWindowsEasyInstaller-v2.0.2-win-arm64.exe.zip)
[![Download for macOS Apple Silicon](https://img.shields.io/badge/macOS%20Apple%20Silicon-Download-343A48?style=for-the-badge&logo=apple&logoColor=white)](https://applejr.ipadownloads.com/signed/NXNativeEasyBuilder-Mac-arm64%202.zip)



<!-- YouTube Full Tutorial Button -->
<div align="center">

  <h2>🎬 Full Installation & Setup Tutorial</h2>

  <p>
    Watch the complete step-by-step guide to port Nintendo Switch games to iOS and iPadOS using NXNative Easy Builder!
  </p>

  <a href="https://youtu.be/Ok-pZ0qQaKk" target="_blank">
    <img
      src="https://img.shields.io/badge/FULL%20TUTORIAL%20YOUTUBE-WATCH%20NOW-FF0033?style=for-the-badge&logo=youtube&logoColor=white&labelColor=161B22"
      alt="Watch Full YouTube Tutorial"
      height="48"
    />
  </a>

  <p>
    <sub>🎮 From Windows setup to building and installing iOS IPA files.</sub>
  </p>

</div>


## Successfully ported to IPA

These titles reached gameplay on an iPhone or iPad in upstream tests.

| Game | Version | Tested device | Result |
| --- | --- | --- | --- |
| Super Mario Odyssey | 1.4.1 | iPhone 17 Pro | [Gameplay observed with NXNative][odyssey] |
| The Legend of Zelda: Tears of the Kingdom | 1.4.3 | A16 iPad | [Windows-built IPA ran and loaded saves][windows-games] |
| Balatro | 1.0 (v0) | A16 iPad | [Windows-built IPA ran][windows-games] |
| Mario vs. Donkey Kong | 1.0.0 (v0) | A16 iPad | [Windows-built IPA ran with slowdowns][windows-games] |

### Game screenshot catalog

All 65 images from the original NXNativeWindows gallery, which covers NXNative tests on Mac or iPhone. [Original gallery][catalog] · [Per-game versions and platforms][game-notes]

<table>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-odyssey.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-odyssey.jpg" width="250" alt="Super Mario Odyssey"></a><br><strong>Super Mario Odyssey</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/zelda-tears-of-the-kingdom.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/zelda-tears-of-the-kingdom.jpg" width="250" alt="The Legend of Zelda: Tears of the Kingdom"></a><br><strong>The Legend of Zelda: Tears of the Kingdom</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/balatro.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/balatro.jpg" width="250" alt="Balatro"></a><br><strong>Balatro</strong></td>
  </tr>
</table>

<details>
<summary><strong>View the other 62 game screenshots</strong></summary>

<table>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/mario-kart-8-deluxe.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/mario-kart-8-deluxe.jpg" width="250" alt="Mario Kart 8 Deluxe"></a><br><strong>Mario Kart 8 Deluxe</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/mario-vs-donkey-kong.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/mario-vs-donkey-kong.jpg" width="250" alt="Mario vs. Donkey Kong"></a><br><strong>Mario vs. Donkey Kong</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/paper-mario-the-thousand-year-door.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/paper-mario-the-thousand-year-door.jpg" width="250" alt="Paper Mario: The Thousand-Year Door"></a><br><strong>Paper Mario: The Thousand-Year Door</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-3d-world.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-3d-world.jpg" width="250" alt="Super Mario 3D World + Bowser&#x27;s Fury"></a><br><strong>Super Mario 3D World + Bowser&#x27;s Fury</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-bros-wonder.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-bros-wonder.jpg" width="250" alt="Super Mario Bros. Wonder"></a><br><strong>Super Mario Bros. Wonder</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-galaxy.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-galaxy.jpg" width="250" alt="Super Mario Galaxy"></a><br><strong>Super Mario Galaxy</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-galaxy-2.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-mario-galaxy-2.jpg" width="250" alt="Super Mario Galaxy 2"></a><br><strong>Super Mario Galaxy 2</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/zelda-breath-of-the-wild.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/zelda-breath-of-the-wild.jpg" width="250" alt="The Legend of Zelda: Breath of the Wild"></a><br><strong>The Legend of Zelda: Breath of the Wild</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/zelda-links-awakening.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/zelda-links-awakening.jpg" width="250" alt="The Legend of Zelda: Link&#x27;s Awakening"></a><br><strong>The Legend of Zelda: Link&#x27;s Awakening</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/13-sentinels.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/13-sentinels.jpg" width="250" alt="13 Sentinels: Aegis Rim"></a><br><strong>13 Sentinels: Aegis Rim</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/absolum.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/absolum.jpg" width="250" alt="Absolum"></a><br><strong>Absolum</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ace-attorney-investigations.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ace-attorney-investigations.jpg" width="250" alt="Ace Attorney Investigations Collection"></a><br><strong>Ace Attorney Investigations Collection</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/advance-wars-1-2-re-boot-camp.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/advance-wars-1-2-re-boot-camp.jpg" width="250" alt="Advance Wars 1+2: Re-Boot Camp"></a><br><strong>Advance Wars 1+2: Re-Boot Camp</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ai-the-somnium-files.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ai-the-somnium-files.jpg" width="250" alt="AI: THE SOMNIUM FILES"></a><br><strong>AI: THE SOMNIUM FILES</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ai-the-somnium-files-nirvana-initiative.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ai-the-somnium-files-nirvana-initiative.jpg" width="250" alt="AI: THE SOMNIUM FILES - nirvanA Initiative"></a><br><strong>AI: THE SOMNIUM FILES - nirvanA Initiative</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/animal-crossing-new-horizons.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/animal-crossing-new-horizons.jpg" width="250" alt="Animal Crossing: New Horizons"></a><br><strong>Animal Crossing: New Horizons</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/animal-well.png"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/animal-well.png" width="250" alt="ANIMAL WELL"></a><br><strong>ANIMAL WELL</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/astral-chain.png"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/astral-chain.png" width="250" alt="ASTRAL CHAIN"></a><br><strong>ASTRAL CHAIN</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ball-x-pit.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ball-x-pit.jpg" width="250" alt="BALL x PIT"></a><br><strong>BALL x PIT</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/bayonetta.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/bayonetta.jpg" width="250" alt="Bayonetta"></a><br><strong>Bayonetta</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/bayonetta-2.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/bayonetta-2.jpg" width="250" alt="Bayonetta 2"></a><br><strong>Bayonetta 2</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/bayonetta-3.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/bayonetta-3.jpg" width="250" alt="Bayonetta 3"></a><br><strong>Bayonetta 3</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/big-brain-academy-brain-vs-brain.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/big-brain-academy-brain-vs-brain.jpg" width="250" alt="Big Brain Academy: Brain vs. Brain"></a><br><strong>Big Brain Academy: Brain vs. Brain</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/brain-memory-training-academy.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/brain-memory-training-academy.jpg" width="250" alt="Brain Memory Training Academy"></a><br><strong>Brain Memory Training Academy</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/captain-toad-treasure-tracker.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/captain-toad-treasure-tracker.jpg" width="250" alt="Captain Toad: Treasure Tracker"></a><br><strong>Captain Toad: Treasure Tracker</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/catherine-full-body.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/catherine-full-body.jpg" width="250" alt="Catherine: Full Body"></a><br><strong>Catherine: Full Body</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/celeste.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/celeste.jpg" width="250" alt="Celeste"></a><br><strong>Celeste</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/cuphead.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/cuphead.jpg" width="250" alt="Cuphead"></a><br><strong>Cuphead</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/dark-souls-remastered-creation.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/dark-souls-remastered-creation.jpg" width="250" alt="DARK SOULS: REMASTERED"></a><br><strong>DARK SOULS: REMASTERED</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/digimon-story-cyber-sleuth.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/digimon-story-cyber-sleuth.jpg" width="250" alt="Digimon Story Cyber Sleuth: Complete Edition"></a><br><strong>Digimon Story Cyber Sleuth: Complete Edition</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/dragon-quest-vii-reimagined.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/dragon-quest-vii-reimagined.jpg" width="250" alt="DRAGON QUEST VII Reimagined"></a><br><strong>DRAGON QUEST VII Reimagined</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/dragon-quest-xi-s.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/dragon-quest-xi-s.jpg" width="250" alt="DRAGON QUEST XI S: Echoes of an Elusive Age – Definitive Edition"></a><br><strong>DRAGON QUEST XI S: Echoes of an Elusive Age – Definitive Edition</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/final-fantasy-resonance.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/final-fantasy-resonance.jpg" width="250" alt="FINAL FANTASY RESONANCE"></a><br><strong>FINAL FANTASY RESONANCE</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/final-fantasy-tactics.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/final-fantasy-tactics.jpg" width="250" alt="FINAL FANTASY TACTICS - The Ivalice Chronicles"></a><br><strong>FINAL FANTASY TACTICS - The Ivalice Chronicles</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/fire-emblem-engage.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/fire-emblem-engage.jpg" width="250" alt="Fire Emblem Engage"></a><br><strong>Fire Emblem Engage</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/fire-emblem-three-houses.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/fire-emblem-three-houses.jpg" width="250" alt="Fire Emblem: Three Houses"></a><br><strong>Fire Emblem: Three Houses</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ghost-trick-phantom-detective.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/ghost-trick-phantom-detective.jpg" width="250" alt="Ghost Trick: Phantom Detective"></a><br><strong>Ghost Trick: Phantom Detective</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/the-great-ace-attorney-chronicles.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/the-great-ace-attorney-chronicles.jpg" width="250" alt="The Great Ace Attorney Chronicles"></a><br><strong>The Great Ace Attorney Chronicles</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hades.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hades.jpg" width="250" alt="Hades"></a><br><strong>Hades</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hades-ii.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hades-ii.jpg" width="250" alt="Hades II"></a><br><strong>Hades II</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hollow-knight-silksong.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hollow-knight-silksong.jpg" width="250" alt="Hollow Knight: Silksong"></a><br><strong>Hollow Knight: Silksong</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hyrule-warriors-age-of-calamity.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/hyrule-warriors-age-of-calamity.jpg" width="250" alt="Hyrule Warriors: Age of Calamity"></a><br><strong>Hyrule Warriors: Age of Calamity</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/luigis-mansion-3.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/luigis-mansion-3.jpg" width="250" alt="Luigi&#x27;s Mansion 3"></a><br><strong>Luigi&#x27;s Mansion 3</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/metroid-dread.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/metroid-dread.jpg" width="250" alt="Metroid Dread"></a><br><strong>Metroid Dread</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/monster-hunter-rise.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/monster-hunter-rise.jpg" width="250" alt="Monster Hunter Rise"></a><br><strong>Monster Hunter Rise</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/monster-train-2.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/monster-train-2.jpg" width="250" alt="Monster Train 2"></a><br><strong>Monster Train 2</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/octopath-traveler-0.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/octopath-traveler-0.jpg" width="250" alt="Octopath Traveler 0"></a><br><strong>Octopath Traveler 0</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/octopath-traveler-ii.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/octopath-traveler-ii.jpg" width="250" alt="Octopath Traveler II"></a><br><strong>Octopath Traveler II</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/patricks-parabox.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/patricks-parabox.jpg" width="250" alt="Patrick&#x27;s Parabox"></a><br><strong>Patrick&#x27;s Parabox</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/persona-5-royal.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/persona-5-royal.jpg" width="250" alt="Persona 5 Royal"></a><br><strong>Persona 5 Royal</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/pikmin-4.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/pikmin-4.jpg" width="250" alt="Pikmin 4"></a><br><strong>Pikmin 4</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/pokemon-legends-z-a.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/pokemon-legends-z-a.jpg" width="250" alt="Pokémon Legends: Z-A"></a><br><strong>Pokémon Legends: Z-A</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/pokemon-sword.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/pokemon-sword.jpg" width="250" alt="Pokémon Sword"></a><br><strong>Pokémon Sword</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/portal.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/portal.jpg" width="250" alt="Portal"></a><br><strong>Portal</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/raging-loop.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/raging-loop.jpg" width="250" alt="Raging Loop"></a><br><strong>Raging Loop</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/rhythm-heaven-groove.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/rhythm-heaven-groove.jpg" width="250" alt="Rhythm Heaven Groove"></a><br><strong>Rhythm Heaven Groove</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/romance-of-the-three-kingdoms-8-remake.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/romance-of-the-three-kingdoms-8-remake.jpg" width="250" alt="ROMANCE OF THE THREE KINGDOMS 8 REMAKE"></a><br><strong>ROMANCE OF THE THREE KINGDOMS 8 REMAKE</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/stardew-valley.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/stardew-valley.jpg" width="250" alt="Stardew Valley"></a><br><strong>Stardew Valley</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/steins-gate-reboot.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/steins-gate-reboot.jpg" width="250" alt="STEINS;GATE RE:BOOT"></a><br><strong>STEINS;GATE RE:BOOT</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-smash-bros-ultimate.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/super-smash-bros-ultimate.jpg" width="250" alt="Super Smash Bros. Ultimate"></a><br><strong>Super Smash Bros. Ultimate</strong></td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/taiko-risshiden-v-dx.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/taiko-risshiden-v-dx.jpg" width="250" alt="Taiko Risshiden V DX"></a><br><strong>Taiko Risshiden V DX</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/unicorn-overlord.jpg"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/unicorn-overlord.jpg" width="250" alt="Unicorn Overlord"></a><br><strong>Unicorn Overlord</strong></td>
  </tr>
</table>

</details>

## Quick start

### macOS

Requires an Apple Silicon Mac with macOS 26.6 or later. The Mac Builder is a preview.

1. Extract the Mac ZIP and open **NXNative Easy Builder.app**.
2. Click **Install Everything** and follow the Xcode or Homebrew setup prompts if shown.
3. Choose your game folder and select `prod.keys` if required. Click **Build IPA** or **Build Mac App**. [Mac guide](macos/README.md)

### Windows

Use the x64 installer on an Intel or AMD PC. The Windows 11 ARM64 build runs the x64 toolchain under emulation and remains experimental.

1. Open the matching EXE and click **Install Everything**.
2. If prompted, select your official Xcode `.xip` in **Apple Setup** and follow the Apple authorization and Metal Developer Tools prompts.
3. Click **Launch NXNativeWindows IPA Builder**. Add your game files and `prod.keys`, then click **Build IPA**. [Windows guide](windows/README.md)

Sign and sideload the finished IPA on your iPhone or iPad.

<details>
<summary><strong>See the original NXNativeWindows IPA Builder</strong></summary>

Builder screenshots from [NXNativeWindows by zenvyzenshiro][nxnativewindows].

<table>
  <tr>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/windows/gui-start.png"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/windows/gui-start.png" width="250" alt="NXNativeWindows IPA Builder ready for game files"></a><br><strong>Choose files</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/windows/gui-building.png"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/windows/gui-building.png" width="250" alt="NXNativeWindows IPA Builder showing build progress"></a><br><strong>Build IPA</strong></td>
    <td align="center" valign="top" width="33%"><a href="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/windows/gui-done.png"><img src="https://raw.githubusercontent.com/zenvyzenshiro/NXNativeWindows/269d61ff974b2e8e073d7357466ae130c3f6812f/docs/images/windows/gui-done.png" width="250" alt="NXNativeWindows IPA Builder showing the finished Balatro IPA"></a><br><strong>IPA ready</strong></td>
  </tr>
</table>

</details>

## Credits

- **iSolution** created the Windows Easy Installer and macOS Builder. [YouTube channel][isolution]
- **seedds** and the contributors created the original NXNative runtime and build pipeline. [NXNative][nxnative]
- **zenvyzenshiro** created the Windows build pipeline and IPA Builder and published the game catalog shown here. [NXNativeWindows][nxnativewindows]

[Windows source license](windows/LICENSE) · [macOS source license](macos/LICENSE)

[nxnative]: https://github.com/seedds/nxnative
[nxnativewindows]: https://github.com/zenvyzenshiro/NXNativeWindows
[isolution]: https://www.youtube.com/@iSolutional
[odyssey]: https://github.com/seedds/nxnative/pull/544
[windows-games]: https://github.com/zenvyzenshiro/NXNativeWindows#games-on-nxnative-for-windows
[catalog]: https://github.com/zenvyzenshiro/NXNativeWindows#upstream-nxnative-games
[game-notes]: https://github.com/zenvyzenshiro/NXNativeWindows/blob/269d61ff974b2e8e073d7357466ae130c3f6812f/dm/games/README.md#games

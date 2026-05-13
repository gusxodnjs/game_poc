# iOS Build Guide — TERRA × BIOSPHERE PoC

This guide walks through building the Unity 6.0 LTS HelloWorld scene to an
iOS device. The Unity script (`Assets/Scripts/HelloWorld.cs`) is already
committed; the remaining work is Unity Editor setup, an Xcode export, and a
device install.

> Total wall time end-to-end: roughly **30–60 min** the first time
> (most of that is Xcode installing from the App Store and Unity's first iOS
> platform switch compile). Subsequent builds are usually 1–3 min.

---

## 0. Prerequisites

| Item | Detail | Status |
| --- | --- | --- |
| macOS host | Apple silicon recommended | ✓ |
| Unity 6.0 LTS | `/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app` | ✓ installed |
| iOS Build Support module | Bundled with the Editor above | ✓ installed |
| **Xcode** | App Store, ~7 GB. Required for the final compile + device install. | ☐ user installs |
| Apple ID | Free Personal Team works (7-day provisioning). Apple Developer Program ($99/yr) lifts the 7-day limit. | ☐ user signs in |
| iOS device | iOS 13+, USB-C / Lightning cable | ☐ user provides |

Install Xcode first: open the App Store, search **Xcode**, click **Get**.
Once installed, launch it once to accept the license, then run:

```bash
sudo xcode-select --switch /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -license accept
```

---

## 1. Open the project in Unity

1. Launch **Unity Hub**.
2. **Projects → Add → Add project from disk** → choose
   `/Users/hyun/projects/game_poc`.
3. Confirm the Editor version is **6000.0.75f1** (Unity Hub auto-selects).
4. Open the project. The first import takes a few minutes while Unity
   generates `Library/`, `Logs/`, etc. (all gitignored).

> If the project does not yet contain `ProjectSettings/`, `Packages/`, or a
> default scene, see PR #10 (`feat/unity-baseline`) for the Unity 2D template
> assets. Merging PR #10 first is recommended.

---

## 2. Attach the HelloWorld script to a scene

The script does not require any prefab, canvas, or asset wiring — it draws
itself via IMGUI as long as a GameObject in the active scene has the
component.

1. **File → Open Scene** → `Assets/Scenes/SampleScene.unity`
   (or whatever scene the 2D template created).
2. In the **Hierarchy** panel, right-click empty space → **Create Empty**.
3. Rename the new GameObject to `HelloRoot`.
4. Drag `Assets/Scripts/HelloWorld.cs` from the Project panel onto
   `HelloRoot` in the Hierarchy. (Or select `HelloRoot`, click
   **Add Component**, type `HelloWorld`, press Enter.)
5. **File → Save** (Cmd+S).
6. Press **▶ Play** in the Editor to verify "Hello, TERRA!" appears centered.
   Stop play mode before continuing.

---

## 3. Switch the build target to iOS

1. **File → Build Profiles** (Unity 6) — older menu name was
   *File → Build Settings*.
2. Select the **iOS** platform on the left.
3. Click **Switch Platform**. First switch takes 1–2 min while Unity
   reimports shaders/textures for iOS.

---

## 4. Configure Player Settings

Open **Edit → Project Settings → Player**, then fill in:

**Identification**

| Field | Value |
| --- | --- |
| Company Name | `TERRA` |
| Product Name | `TerraPoC` |
| Bundle Identifier | `com.gusxodnjs.terrapoc` (must be globally unique) |
| Version | `0.1.0` |
| Build | `1` |

**iOS → Other Settings**

| Field | Value |
| --- | --- |
| Target minimum iOS Version | `13.0` |
| Target Device | `iPhone Only` (or `iPhone + iPad`) |
| Scripting Backend | `IL2CPP` |
| Api Compatibility Level | `.NET Standard 2.1` |
| Architecture | `ARM64` |
| Camera Usage Description | leave empty unless you add a camera-using feature |

> The bundle identifier must be unique across **everything signed by your
> Apple ID**. If your free Personal Team has already used
> `com.gusxodnjs.terrapoc`, change the suffix (e.g. `terrapoc2`).

---

## 5. Build the Xcode project from Unity

1. Back in **Build Profiles** → click **Build**.
2. Choose an output directory. Recommended: `build/ios/`
   (`build/` is gitignored).
3. Wait for the IL2CPP transpile + asset bundle step.
   On Apple silicon this is typically 1–3 min for a HelloWorld project.
4. When finished, Unity opens the output folder in Finder.
   You will see `Unity-iPhone.xcodeproj` and several support folders.

---

## 6. Open the Xcode project and sign

1. Double-click `Unity-iPhone.xcodeproj` → Xcode launches.
2. In the **Project Navigator** (left sidebar), select the top-level
   `Unity-iPhone` project.
3. Select the `Unity-iPhone` **target** in the central pane.
4. Open the **Signing & Capabilities** tab.
   - Check **Automatically manage signing**.
   - **Team** → choose your *Personal Team* (Apple ID).
     If the dropdown is empty: **Xcode → Settings → Accounts**, click `+`,
     **Apple ID**, sign in. Return to the target panel and pick the team.
   - **Bundle Identifier** → must match what you set in Unity. If Xcode
     complains "An App ID with Identifier ... is not available", change it
     to something unique (e.g. append your initials).

---

## 7. Install on the device

1. Connect the iPhone via USB. The first time, accept *Trust This Computer*
   on the device.
2. In Xcode's top bar, click the **scheme/device** dropdown next to the
   ▶ button → pick your iPhone.
3. Click **▶ (Run)**. Xcode compiles the IL2CPP-generated C++, signs the
   app, copies it to the device, and launches it.
4. **On the device, first launch**:
   - You may see *"Untrusted Developer"* → on iPhone:
     **Settings → General → VPN & Device Management → [your Apple ID]
     → Trust**.
   - Launch *TerraPoC* from the home screen.
5. You should see **"Hello, TERRA!"** centered on screen.

---

## 8. Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Xcode: `No signing certificate "iOS Development" found` | No team selected, or Apple ID not added | Xcode → Settings → Accounts → add Apple ID, then pick team on target |
| Xcode: `Failed to register bundle identifier` | Bundle ID already in use on your Apple ID | Change Bundle Identifier to a unique value (Unity Player Settings AND Xcode) |
| Xcode: `Build for testing failed` | Mixed cause; check the issue navigator | Usually re-running Run after a clean build folder (Product → Clean Build Folder) resolves |
| Black screen on device | Older `OnGUI` rendering with a dark skybox | The committed `HelloWorld.cs` includes a shadow + auto-scaling font; if you customized, restore defaults |
| `Untrusted Developer` modal on launch | First time installing apps from this Apple ID | iOS Settings → General → VPN & Device Management → Trust |
| App installs but quits immediately | Provisioning expired, or device unplugged mid-install | Re-run from Xcode while device is connected |
| Unity build error: *"Switch Platform" stuck* | iOS module not loaded | Unity Hub → Installs → ⚙ on the Editor → Add Modules → iOS Build Support |

---

## 9. The 7-day Personal Team limit

Apps signed with a free Apple ID (Personal Team) **stop launching after
7 days**. To extend:

- **Same flow, again**: connect the device to Xcode and Run. The newly
  signed install gets another 7-day window.
- **Permanent fix**: enroll in the
  [Apple Developer Program](https://developer.apple.com/programs/)
  ($99/yr) → certificates last 1 year and TestFlight builds are 90 days.

---

## 10. What to verify

The Step 1 acceptance criterion (Genesis directive §2-1) is:

> "iOS device displays *Hello, TERRA!* from a Unity 6.0 LTS build."

Capture a quick photo or screen recording of the device showing the text and
attach it to issue #1 when done.

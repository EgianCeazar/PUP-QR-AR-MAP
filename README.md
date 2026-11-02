# PUP Map: QR + AR Campus Navigation

Phase 1: map data, route finding, and a 2D map you can test in the Unity Editor.

## What's in here

```
Assets/
  StreamingAssets/Maps/pup_main.json   <- the campus map (PLACEHOLDER coordinates)
  PUPMap/Scripts/Core/        <- pure C#, no Unity. Map models, GPS math, A*, QR format
  PUPMap/Scripts/Unity/       <- loads the map, draws it, handles UI and camera
  PUPMap/Scripts/Editor/      <- "PUP Map > Build Phase 1 Scene" menu
Tools/make_qr_sheet.py <- prints one QR code per checkpoint as a PDF
```

## Setup (Unity 6 LTS)

1. Install **Unity 6 LTS** from Unity Hub and add the **Android Build Support** module.
2. Create a new project with the **Universal 3D** template.
3. (Already done) Scripts live in `Assets/PUPMap`, map data in `Assets/StreamingAssets`.
4. Go to **Edit > Project Settings > Player > Other Settings** and set **Active Input Handling** to **Both**. The camera script uses the old Input API for now.
5. In the top menu, click **PUP Map > Build Phase 1 Scene**.
6. Press **Play**. Pick "You are here" and "Go to", and the gold line shows the route.
7. Test a fake scan by typing `WAYFIND:1:pup-main:lagoon` in the box and clicking **Use QR**.

Controls: drag to pan, scroll or pinch to zoom.

## 2D map or AR

The buttons at the top switch between **2D Map** and **AR View**. The app remembers which one the user picked.

- 2D Map is the default, and it always works.
- AR View stays greyed out ("soon") until Phase 2 sets `ViewModeController.IsArSupported`. After that, phones that can't run ARCore will still only get the 2D map.
- Both modes share the same route, so switching modes never loses your place.

## The map file

- **nodes** are spots on campus. `isDestination: false` makes a spot a junction that doesn't show up in the "Go to" list.
- **edges** are walkable paths, and you can walk them both ways. `accessible: false` means the path has stairs. `distance` is optional and gets calculated from the GPS coordinates if you leave it out.
- **facing** is the compass direction a person looks when scanning that QR code (0 = north, 90 = east). Phase 2 uses it to point the AR arrows the right way.

**To replace the placeholders:** stand at each spot and read your GPS coordinates from Google Maps (long-press on your blue dot). Use a compass app to get the facing direction, then update the JSON. If something's wrong, the Unity Console shows warnings for broken data, like unknown ids or unreachable spots.

## QR codes

Every QR code holds `WAYFIND:1:<campusId>:<nodeId>`. The campus id is included so the same app can support other campuses later, which sets you up for the wayfinding kit.

```
pip install qrcode reportlab
python Tools/make_qr_sheet.py Assets/StreamingAssets/Maps/pup_main.json qr_sheet.pdf
```

## Next: Phase 2

- Real QR scanning with the camera (ZXing.Net or ML Kit) that calls `NavigationController.SetStartFromQr(text)`
- AR Foundation + ARCore: a scan anchors your position and `facing`, then the app places 3D arrows along `route.steps`
- Re-scan at checkpoints to fix drift

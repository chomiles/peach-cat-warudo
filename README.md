# Peach Cat for Warudo

[한국어](README_KO.md) · [日本語](README_JA.md) · English

Peach Cat is a floating cat prop for Warudo, inspired by the motion of the character in Grok. It is an independent project.

![Peach Cat animation preview](media/peach-cat.gif)

## What it does

- Reacts to ARKit52 facial tracking with smiling, sad, and angry eyes. A sustained smile makes it bounce.
- Follows head and eye direction. When idle, it looks around, blinks, changes size, spins, flips, and sometimes opens round eyes.
- When a chat event arrives, it looks down and scans from side to side.
- You can change the body, blush, and eye colors, tracking sensitivity, reaction timing, idle frequency, and chat reading motion in Warudo.

## Install

1. Download **PeachCat-for-Warudo-1.5.0.zip** from [Releases](https://github.com/chomiles/peach-cat-warudo/releases).
2. In Warudo, choose **Menu → Open data folder**. Extract the ZIP and merge its **Props** and **Playground** folders into that directory.
3. Add a **Prop**, select **PeachCat** under **Source**, and save your scene. Start at scale 1.
4. Open **Settings → Peach Cat Reactions** for customization and preview buttons.

## Face tracking and other avatars

Set up Warudo's [iFacialMocap receiver](https://docs.warudo.app/docs/mocap/ifacialmocap) and enable the **Face Tracking - iFacialMocap** blueprint. Peach Cat reads the active receiver's ARKit coefficients and head and eye rotations, so changing the avatar does not require remapping its meshes or blendshapes. This release targets the iFacialMocap receiver in Warudo. Other ARKit52 trackers may require an adapter. With no active tracking, idle motion still works.

## Connect chat

- **Blueprint** · Connect a compatible chat receiver node's flow output to **Peach Cat Read Chat → Enter**. Enable **React to Chat Events** and turn off **Use Local Overlay Feed** for this setup. To test without a chat receiver, connect a hotkey flow or use **Preview Chat Reading**.
- **HTTPS SSE** · If your own relay sends `event: chat` with JSON containing `"platform":"chzzk"`, put its HTTPS `/events` URL on one line in `PeachCatChatUrl.txt` at the root of the Warudo data folder. Enable **Use Local Overlay Feed** and **React to Chat Events**. Toggle the feed off and on after changing the URL. Keep the URL file private.
- **Official CHZZK API** · An external receiver must obtain broadcaster OAuth consent with the chat-message read scope, open a Socket.IO session, and subscribe to `CHAT`. Route its events into either input above. The download contains the Warudo integration. Set up the OAuth login and chat receiver separately. [CHZZK Session API](https://chzzk.gitbook.io/chzzk/chzzk-api/session)

## Edit the model

`Data/PeachCatEditable.unitypackage` includes the prefab, materials and idle animation. Import it into a Built-in Render Pipeline project with lilToon installed. The prefab is `Assets/PeachCat/Prop.prefab`. The tested authoring setup was Unity 2022.3.22f1 with lilToon 2.3.4 and a Warudo-compatible prop exporter.

`Data/PeachCatBody.obj` contains the face and ear mesh for Blender. The model generator in `Data/Editor` can be copied under Unity's `Assets/Editor`. Edit its dimensions and run **Tools → Peach Cat → Generate Editable Prefab**.

To use a modified model, export the `Prop` prefab with the Warudo SDK. Keep the `PeachCat.warudo` filename and the `Bounce/Gaze/Eyes` and `Face and ears` hierarchy used by the script. For major changes to the face, adjust the eye positions and cheek projection as needed.

## License

The original code, model, and motion assets are available under [MIT](LICENSE). lilToon and its bundled components retain their [original notices](Licenses/).

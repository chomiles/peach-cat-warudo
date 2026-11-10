# Peach Cat for Warudo

한국어 · [日本語](README_JA.md) · [English](README.md)

Grok 캐릭터의 모션에서 영감을 받아 만든 독립 와루도 고양이 소품입니다.

![Peach Cat 움직임 미리보기](media/peach-cat.gif)

## 기능

- ARKit52 얼굴 추적에 맞춰 웃는 눈, 시무룩한 눈, 화난 눈으로 바뀝니다. 웃음이 이어지면 좌우로 통통 뜁니다.
- 고개와 눈동자 방향을 따라봅니다. 평소에는 주변을 둘러보고, 깜빡이고, 크기가 바뀌고, 좌우 회전이나 공중제비를 하거나 눈을 동그랗게 뜹니다.
- 채팅 이벤트를 받으면 고개를 숙여 좌우로 훑어보는 모션을 재생합니다.
- 와루도 설정에서 바디·홍조·눈 색, 추적 감도, 반응 타이밍, 대기 동작 빈도, 채팅 읽기 모션을 조절할 수 있습니다.

## 설치

1. [Releases](https://github.com/chomiles/peach-cat-warudo/releases)에서 **PeachCat-for-Warudo-1.5.0.zip**을 받습니다.
2. 와루도에서 **Menu → Open data folder**를 열고, ZIP을 풀어 **Props**와 **Playground** 폴더를 데이터 폴더에 합칩니다.
3. **Prop**을 추가하고 **Source**에서 **PeachCat**을 선택한 뒤 씬을 저장합니다. 배율은 1부터 시작하면 됩니다.
4. **Settings → Peach Cat Reactions**에서 설정과 미리보기 버튼을 찾을 수 있습니다.

## 얼굴 추적과 다른 아바타

와루도에서 [iFacialMocap Receiver](https://docs.warudo.app/docs/mocap/ifacialmocap)를 설정하고 **Face Tracking - iFacialMocap** 블루프린트를 켭니다. 고양이는 활성 수신기의 ARKit 계수와 고개·눈 회전값을 읽으므로 아바타를 바꿔도 메시나 블렌드셰이프를 다시 지정할 필요가 없습니다. 이 버전은 와루도의 iFacialMocap 수신기에 연결됩니다. 다른 ARKit52 추적기는 별도 어댑터가 필요할 수 있습니다. 추적이 없을 때도 대기 모션은 작동합니다.

## 채팅 연결

- **블루프린트** · 호환되는 채팅 수신 노드의 흐름 출력을 **Peach Cat Read Chat → Enter**에 연결합니다. **React to Chat Events**를 켜고 **Use Local Overlay Feed**를 끕니다. 핫키 노드를 연결하거나 **Preview Chat Reading**을 눌러 채팅 없이도 동작을 확인할 수 있습니다.
- **HTTPS SSE** · 본인의 서버가 `"platform":"chzzk"`가 들어 있는 JSON을 `event: chat`으로 보낸다면, 와루도 데이터 폴더 최상단의 `PeachCatChatUrl.txt`에 HTTPS `/events` 주소를 한 줄로 넣습니다. **Use Local Overlay Feed**와 **React to Chat Events**를 켭니다. 주소를 바꾼 뒤에는 피드를 껐다 켜세요. 이 주소 파일은 비공개로 보관합니다.
- **치지직 공식 API** · 별도 수신기가 방송 계정의 OAuth 동의와 **채팅 메시지 조회** 권한을 받아 Socket.IO 세션에서 `CHAT`을 구독해야 합니다. 그 이벤트를 위 두 입력 중 하나에 연결합니다. 배포 파일은 와루도 연결부를 제공합니다. 치지직 로그인과 공식 API 수신기는 별도로 준비합니다. [치지직 세션 API](https://chzzk.gitbook.io/chzzk/chzzk-api/session)

## 모양 편집

`Data/PeachCatEditable.unitypackage`에는 프리팹, 재질, 대기 애니메이션이 들어 있습니다. lilToon이 설치된 Unity Built-in Render Pipeline 프로젝트에 가져온 뒤 `Assets/PeachCat/Prop.prefab`을 엽니다. 제작 환경은 Unity 2022.3.22f1, lilToon 2.3.4, 와루도 호환 소품 빌더입니다.

`Data/PeachCatBody.obj`는 Blender에서 편집할 수 있는 얼굴과 귀 메시입니다. `Data/Editor`의 생성기를 Unity 프로젝트의 `Assets/Editor` 아래에 복사하면 치수를 수정한 뒤 **Tools → Peach Cat → Generate Editable Prefab**으로 다시 만들 수 있습니다.

수정한 모델을 와루도에서 쓰려면 SDK로 `Prop` 프리팹을 다시 빌드합니다. 스크립트가 찾는 `PeachCat.warudo` 파일명과 `Bounce/Gaze/Eyes`, `Face and ears` 구조를 유지하세요. 얼굴 곡률을 크게 바꾸면 눈 위치와 홍조 투영 반경도 조정할 수 있습니다.

## 라이선스

직접 제작한 코드, 모델, 모션은 [MIT 라이선스](LICENSE)로 제공합니다. lilToon과 그 구성요소에는 [기존 라이선스 및 고지](Licenses/)가 적용됩니다.

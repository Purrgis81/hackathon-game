# hackathon-game
해커톤용 단기 게임 개발

## 침묵의 석실 · StoneChamber

Unity 3D 타일 회피 게임입니다. 혼자 플레이하거나 초대 코드로 2~5인 온라인 대전을 할 수 있습니다.

## 개발 환경

- Unity Editor **6000.3.13f1**
- Built-in Render Pipeline
- Unity Transport 2.7.5
- Multiplayer Services 2.5.0

## 프로젝트 실행

처음 Git을 사용하는 팀원은 아래 GitHub Desktop 순서로 진행하면 됩니다. 공개 저장소의 코드는 누구나 받을 수 있으며, 이 저장소에 직접 업로드할 팀원은 초대받은 GitHub 계정으로 협업 초대를 수락합니다.

### 1. 개발 도구 준비

- GitHub Desktop을 설치하고 본인의 GitHub 계정으로 로그인합니다.
- Unity Hub에서 Unity Editor **6000.3.13f1**을 설치합니다. Hub 목록에서 찾기 어려우면 Unity 다운로드 아카이브에서 해당 버전을 찾습니다.
- C#을 수정할 편집기인 Visual Studio 또는 Rider 등을 준비합니다.

[GitHub Desktop 다운로드](https://desktop.github.com/download/), [Unity Hub 다운로드](https://unity.com/download), [Unity Editor 아카이브](https://unity.com/releases/editor/archive)

### 2. 저장소 내려받기

1. GitHub Desktop에서 **File → Clone repository…**를 누릅니다.
2. **URL** 탭에 아래 주소를 입력합니다.
3. **Local Path**에서 프로젝트를 저장할 위치를 고르고 **Clone**을 누릅니다. 예를 들어 최종 폴더가 `D:\Projects\hackathon-game`이 되도록 지정할 수 있습니다.

```text
https://github.com/Purrgis81/hackathon-game.git
```

수정과 동기화를 이어갈 프로젝트는 이처럼 Clone으로 받습니다. 내려받은 폴더를 열면 `Assets`, `Packages`, `ProjectSettings`가 함께 있어야 합니다. [GitHub Desktop 클론 안내](https://docs.github.com/en/desktop/adding-and-cloning-repositories/cloning-and-forking-repositories-from-github-desktop)

### 3. Unity에서 실행

1. Unity Hub의 **Projects**에서 **Add / Add project from disk**로 기존 프로젝트를 추가합니다. 버튼 표기는 Hub 버전에 따라 조금 다를 수 있습니다.
2. 앞에서 클론한 `hackathon-game` 폴더 전체를 선택합니다. `Assets`, `Packages`, `ProjectSettings`가 들어 있는 상위 폴더입니다.
3. Unity **6000.3.13f1**로 열고 패키지 설치와 에셋 임포트가 끝날 때까지 기다립니다. 첫 실행에는 인터넷 연결이 필요합니다.
4. Unity의 Project 창에서 `Assets → Scenes → StoneChamber` 장면을 더블클릭합니다.
5. 상단 **▶ Play**를 누르고 **Game** 창을 클릭해 키 입력을 받습니다.

착지 연출 후 방향키 또는 WASD로 이동합니다. 첫발을 내딛기 전에 이름과 색상을 설정하거나 멀티플레이 방을 선택할 수 있습니다.

[Unity Hub 프로젝트 관리 안내](https://docs.unity.com/en-us/hub/projects)

## 멀티플레이

방장은 **친구와 UDP 대전 → 온라인 방 만들기**를 선택하고 표시된 초대 코드를 공유합니다. 참가자는 코드를 입력하고 **초대 코드로 참가**를 선택합니다. 5명이 모이면 자동 시작하며 2~4명은 방장이 시작 버튼을 누릅니다.

Unity Cloud의 StoneChamber 프로젝트와 Relay가 연결되어 있습니다. 플레이어는 익명 인증으로 접속하며 게임 안에서 Unity 계정으로 로그인할 필요가 없습니다.

같은 네트워크에서는 IP 직접 접속도 지원합니다. 직접 접속 포트는 UDP 47737입니다. 모든 참가자는 같은 버전을 사용해야 합니다.

시뮬레이션은 120Hz입니다. 방장이 이동·점유·피격을 확정하며 참가자는 이동 예측과 보간을 사용합니다. 이동 데이터는 UDP로 전송하고, 방 구성과 시작 신호는 별도 신뢰성 채널로 전달합니다.

## 현재 게임 규칙

- 발판은 10×10에서 시작해 3×3까지 줄어듭니다.
- 가시는 순차 전개, 양방향 나선, 전역 공격 패턴으로 나옵니다.
- 화살은 사방·팔방에서 시차를 두고 발사됩니다.
- 독은 짧게 남으며 십자·직선·별 패턴으로 퍼집니다.
- 파도는 캐릭터를 가장자리까지 밀고 기절시킵니다. 방향키 6회로 해제하며 입력이 없으면 4초 뒤 회복합니다.
- 용암은 별도 공격 구간에서 2~4칸을 차례로 타격합니다.
- 승강 발판의 민트색은 왕복 안전 구역, 주황색은 하층 화살 위험 구역, 분홍색은 복귀 화살 위험 구역입니다.
- 상대가 점유한 칸은 막히며, 마지막 생존자가 라운드를 이깁니다.

## 주요 코드

| 파일 | 담당 |
| --- | --- |
| `Assets/Scripts/ChamberGame.cs` | 게임 진행, 패턴 선택, 피격 판정과 라운드 |
| `Assets/Scripts/ChamberLan.cs`, `ChamberDatagrams.cs`, `ChamberRelay.cs` | 방 구성, UDP 전송, Relay 접속 |
| `Assets/Scripts/ChamberRunner.cs`, `ExplorerHop.cs` | 타일 이동, 점유, 밀림·기절과 모션 |
| `Assets/Scripts/RisingBoard.cs` | 맵 축소와 승강 발판 |
| `Assets/Scripts/ArrowVolley.cs`, `TidalWave.cs`, `LavaBarrage.cs` | 화살, 파도와 용암 패턴 |
| `Assets/Resources` | 런타임 셰이더와 효과음 |

## 함께 개발하기

아래는 기능별 브랜치에서 수정한 뒤 `main`에 합치는 권장 작업 흐름입니다. 브랜치는 각자의 작업 공간이고, Pull Request는 수정 내용을 함께 확인하고 합치는 요청입니다.

### 1. 작업 시작 전에 최신 코드 받기

1. 이전에 작업하던 내용이 있으면 Unity에서 저장하고 현재 작업 브랜치에 커밋합니다.
2. Unity 프로젝트를 닫은 상태에서 GitHub Desktop의 **Current Branch**를 `main`으로 선택합니다.
3. **Fetch origin**을 누르고, **Pull origin**이 나타나면 눌러 팀원들이 합친 최신 코드를 받습니다.
4. **Current Branch → New Branch**에서 이번 작업용 브랜치를 만듭니다. 예를 들어 `feature/lava-balance`, `feature/player-motion`, `fix/arrow-warning`처럼 작업 내용을 이름으로 씁니다.
5. 최신 `main`을 기준으로 브랜치를 만든 뒤 Unity 프로젝트를 다시 엽니다.

[브랜치 만들기](https://docs.github.com/en/desktop/making-changes-in-a-branch/managing-branches-in-github-desktop), [Fetch와 Pull 안내](https://docs.github.com/en/desktop/working-with-your-remote-repository-on-github-or-github-enterprise/syncing-your-branch-in-github-desktop)

### 2. Unity와 편집기에서 수정

1. Play를 끄고 C# 코드, 장면, 프리팹이나 에셋을 수정합니다.
2. 변경한 파일을 저장합니다. 장면은 **Ctrl+S**로 저장할 수 있습니다.
3. 게임 장면에서 Play를 눌러 동작을 확인하고 Console의 빨간 오류를 해결합니다.
4. 확인이 끝나면 Play를 끄고 **File → Save Project**로 프로젝트 설정을 저장합니다.

에셋을 추가하거나 옮기면 해당 `.meta` 파일도 함께 커밋합니다. 에셋 이동과 이름 변경은 Unity Editor에서 하면 참조를 보존하기 쉽습니다. 장면 파일은 텍스트 직렬화, 버전 관리는 Visible Meta Files로 설정되어 있습니다.

### 3. 수정 내용을 GitHub에 올리기

1. GitHub Desktop의 **Changes**에서 이번 작업에 해당하는 파일과 에셋의 `.meta`를 확인합니다.
2. **Summary**에 변경 내용을 적습니다. 예를 들어 `용암 예고 시간을 늘리고 타격 수 조정`처럼 씁니다.
3. **Commit to 작업브랜치**를 눌러 내 컴퓨터의 Git에 변경 이력을 저장합니다.
4. **Publish branch**를 눌러 새 브랜치를 처음 올립니다. 이미 올린 브랜치에는 **Push origin**을 눌러 후속 커밋을 전송합니다.
5. GitHub 저장소의 브랜치 목록에서 자신의 브랜치와 최신 커밋이 보이는지 확인합니다.

Commit은 로컬 이력 저장, Push는 GitHub 전송입니다. 두 단계를 마치면 다른 팀원도 해당 브랜치의 수정 내용을 받을 수 있습니다. Push 전에 Desktop이 Pull을 안내하면 원격 변경을 먼저 가져오고 충돌 여부를 확인합니다. [커밋 안내](https://docs.github.com/en/desktop/making-changes-in-a-branch/committing-and-reviewing-changes-to-your-project-in-github-desktop), [업로드 안내](https://docs.github.com/en/desktop/making-changes-in-a-branch/pushing-changes-to-github-from-github-desktop)

### 4. 팀의 main에 합치기

1. 업로드한 작업 브랜치에서 **Preview Pull Request → Create Pull Request**를 누릅니다. 브라우저에서 저장소의 **Pull requests → New pull request**로 시작해도 됩니다.
2. 합칠 대상인 **base**를 `main`, 변경 내용을 가져올 **compare**를 자신의 작업 브랜치로 선택합니다.
3. 제목과 설명에 무엇을 바꿨고 어떻게 테스트했는지 적고 Pull Request를 생성합니다.
4. 팀원이 변경 내용을 확인한 뒤 **Merge pull request**로 `main`에 합칩니다.
5. 각 팀원은 Unity 프로젝트를 저장하고 닫은 뒤 `main`으로 전환해 **Fetch origin → Pull origin**으로 합쳐진 내용을 받습니다.

[Pull Request 만들기](https://docs.github.com/en/desktop/working-with-your-remote-repository-on-github-or-github-enterprise/creating-an-issue-or-pull-request-from-github-desktop)

### 작업 중 main의 변경이 필요할 때

현재 작업을 커밋한 뒤 `main`으로 전환해 Fetch와 Pull을 합니다. 자신의 작업 브랜치로 돌아와 **Current Branch → Choose a branch to merge into…**에서 `main`을 골라 합칩니다. 충돌이 나타나면 겹친 파일을 수정한 팀원과 두 변경을 확인하고, 해결한 결과를 커밋합니다.

같은 장면이나 프리팹을 함께 수정할 때는 작업 범위를 먼저 공유하면 충돌을 줄일 수 있습니다. 네트워크 메시지나 판정을 바꾸면 같은 버전의 클라이언트 여러 개로 함께 검증합니다.

### 문제가 생겼을 때

| 증상 | 확인할 내용 |
| --- | --- |
| 업로드에서 권한 오류가 남 | GitHub Desktop의 로그인 계정과 협업 초대 수락 여부를 확인합니다. 공개 저장소도 직접 Push하려면 쓰기 권한이 필요합니다. |
| Unity Hub가 프로젝트를 인식하지 못함 | 선택한 폴더 바로 아래에 `Assets`, `Packages`, `ProjectSettings`가 있는지 확인합니다. |
| 장면이 비어 있거나 다른 화면이 나옴 | `Assets/Scenes/StoneChamber.unity`를 엽니다. |
| C# 컴파일 오류가 남 | Console의 첫 번째 빨간 오류를 확인합니다. 사용 중인 Unity 버전과 최근 수정 파일을 함께 확인합니다. |
| Pull 또는 병합에서 충돌이 남 | 현재 변경을 저장·커밋한 상태에서 겹친 내용을 확인하고 해결합니다. 충돌 해결 후 다시 게임을 실행해 확인합니다. |

Unity가 생성하는 캐시·로그·개인 설정과 실행 빌드는 Git에서 제외됩니다. 임포트에 필요한 소스 에셋과 패키지 잠금 파일은 포함됩니다.

## 외부 에셋과 라이선스

아트의 배포 조건은 `Assets/Art/Kenney/License.txt`에 있습니다. 음악과 효과음의 출처, 수정 내역, CC0 및 CC BY 3.0 표기는 `AudioCredits/README.md`와 같은 폴더의 라이선스 파일을 확인하세요.

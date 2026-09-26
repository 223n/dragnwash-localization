# Drag'n Wash Localization

[English](README.md) | [日本語](README.ja.md)

> [!NOTE]
> 이 문서는 영어판 [README.md](README.md)를 번역한 거예요. 내용이 다르면 영어판이 최신이에요.

[Drag'n Wash](https://store.steampowered.com/app/4739660/)용 비공식 다국어 모드예요. BepInEx로 동작해요.

게임을 여러 언어로 플레이할 수 있고([Language packs](#language-packs) 참고), 코드를 몰라도 CSV 파일만 고치면 누구나 언어를 추가하거나 다듬을 수 있어요.

기술 조사와 구현 계획은 [docs/PLAN.md](docs/PLAN.md)에 정리해 두었어요.

> [!WARNING]
> ## ⚠️ 스포일러 경고 ⚠️
> **`Translations/` 아래 CSV 파일에는 게임의 모든 대사가 스토리 순서대로 들어 있어요.**
> 열어 보면 스포일러를 그대로 보게 돼요. 게임을 몇 회차 먼저 플레이한 다음에 보세요!

## 설치

### 빠른 설치 (권장)

설치는 정말 쉬워요.

1. [Releases 페이지](https://github.com/TomXV/dragnwash-localization/releases)에서 zip을 받아 아무 곳에나 압축을 풀어요.
2. **`Install.exe`**를 더블클릭해요.
3. 언어를 고르고 **Install**을 눌러요. 이미 설치했다면 **Update**를 누르면 돼요.

> [!TIP]
> 같은 방법을 Steam 가이드에도 적어 두었어요: [English](https://steamcommunity.com/sharedfiles/filedetails/?id=3801420947) / [日本語](https://steamcommunity.com/sharedfiles/filedetails/?id=3801418794). Drag'n Wash는 Steam Workshop을 지원하지 않아서 모드는 GitHub Releases에서 받아야 해요.

## 봤죠? 쉽습니다. ( ･´ｰ･｀) HEH! YIP!

설치 프로그램이 Steam에서 게임 위치를 알아서 찾아 주고, 폴더를 직접 고를 수도 있어요. BepInEx가 아직 없으면 공식 5.4.23.5 릴리스를 받아 SHA-256을 확인한 다음 압축까지 알아서 풀어 줘요. 모드가 쓰는 Drag'n Wash ModFramework도 게임 폴더에 충분히 새로운 버전이 없으면 같은 식으로 프레임워크의 GitHub 릴리스에서 받아 와요. 받기 전에 먼저 물어봐요. 그다음엔 Steam에서 게임을 실행하기만 하면 돼요.

고를 수 있는 언어는 日本語 / 简体中文 / English(번역 없음), 원어민이 교정한 한국어, 원어민이 번역한 튀르키예어, 번체 중국어, 독일어, 프랑스어, 스페인어, 브라질 포르투갈어, 러시아어, 폴란드어, 히브리어, 우크라이나어, 태국어, 베트남어 임시 팩, 그리고 재미로 넣은 에스페란토와 토키 포나예요([Language packs](#language-packs) 참고). 같은 창에 **Uninstall** 버튼도 있어요. 기본적으로 세이브 히스토리 스냅샷은 남겨 두고, BepInEx는 직접 요청했고 다른 모드가 쓰지 않을 때만 모드와 같이 지워요. 게임 안에서도 지울 수 있는데, **Options → Mods → Drag'n Wash Localization → Uninstall**을 누르면 다음에 게임을 시작할 때 모드가 제거돼요.

직접 설치하고 싶다면 아래 수동 설치 방법을 따라 하세요.


> [!NOTE]
> **`Install.exe`를 실행해도 아무 반응이 없거나 "Windows에서 PC를 보호했습니다"라고 나올 때**
> `Install.exe`는 서명이 없는 작은 프로그램이라서, 처음 실행할 때 Windows SmartScreen이 막을 수 있어요.
> - 경고가 뜨면 **추가 정보 → 실행**을 누르세요.
> - 창이 아예 안 뜨면 `Install.exe`를 우클릭 → 속성 → **차단 해제**에 체크 → 확인을 누른 다음 다시 더블클릭하세요.

> [!WARNING]
> **Windows 보안(Microsoft Defender)이 `Install.exe`를 "Trojan:Script/Wacatac.B!ml"로 잡았거나, 압축을 푼 폴더에서 `Install.exe`가 사라졌을 때**
> 이건 오탐지(false positive)예요. `!ml`이 붙은 건 머신러닝 모델이 파일을 수상하다고 추측했다는 뜻이고, 알려진 악성코드와 일치했다는 뜻은 아니에요. v1.0.0까지의 설치 프로그램은 콘솔 창 없이 PowerShell 스크립트를 실행했는데, 이게 악성코드가 하는 동작과 비슷해 보였어요. v1.1.0부터 `Install.exe`는 Drag'n Wash ModFramework의 공용 설치 프로그램이고, 스크립트를 실행하지 않는 평범한 무서명 프로그램이에요. 그래도 새로 나온 무서명 파일은 여전히 탐지될 수 있어요. 소스는 프레임워크 저장소의 [`installer/`](https://github.com/TomXV/dragnwash-modframework/tree/main/installer)에 공개되어 있어요.
>
> 파일이 자동으로 격리되면 위협 이름이 안 뜨고, 압축을 푼 폴더에서 `Install.exe`가 그냥 없는 것처럼 보이기도 해요. 뭐가 지워졌는지는 Windows 보안 → **보호 기록**에서 확인할 수 있어요.
> - 먼저 받은 zip이 진짜인지 확인하세요. PowerShell에서 `(Get-FileHash "<zip 경로>").Hash -eq ("<Releases의 sha256>" -replace '^sha256:')`를 실행해서 `True`가 나오면 여기서 배포한 파일과 같은 파일이에요. `False`가 나오면 파일을 지우고 쓰지 마세요.
> - `sha256`은 [Releases](https://github.com/TomXV/dragnwash-localization/releases) 페이지의 `DragNWashLocalization-<version>.zip` 항목 아래에 나와 있어요. 자동으로 생기는 "Source code" 항목 2개에는 해시가 없으니 쓰지 마세요.
> - 해시가 맞으면 여기서 배포한 파일이라는 건 확인돼요. 다만 그것만으로 안전하다고 완전히 증명되는 건 아니니, 그 부분은 위에 링크한 소스를 확인해 보세요.
> - 해시가 맞으면 Windows 보안 → **보호 기록**에서 그 탐지 항목을 열고 **동작 → 디바이스에서 허용**을 고르세요. 그 파일 하나만 허용하면 되고, 폴더 제외를 추가하거나 Windows 보안을 끌 필요는 없어요.
> - 아무것도 허용하고 싶지 않다면 아래 수동 설치 방법을 쓰세요.
> - 이 저장소의 Releases 페이지가 아닌 곳에서 받은 파일은 쓰지 마세요.

### Windows on ARM (검증됨)

Snapdragon X 노트북 같은 ARM Windows PC에서도 똑같이 `Install.exe`로 설치하면 동작해요. 게임 자체는 에뮬레이션으로 x64로 실행돼요.

> [!IMPORTANT]
> **이 환경에서는 게임이 DirectX 12로 제대로 그려지지 않으니, Steam 실행 옵션에 `-force-d3d11`을 추가하세요.** 모드가 있든 없든 생기는 게임 쪽 문제예요.
> - 오래된 GPU 드라이버에서는 스플래시 화면이 끝나자마자 게임이 크래시해요.
> - 최신 드라이버에서는 크래시는 줄었지만 3D가 안 그려져요.
>
> Steam에서 게임을 우클릭 → **속성** → **실행 옵션**에 `-force-d3d11`을 넣으면 DirectX 11로 실행돼서 문제없이 플레이할 수 있어요.
> ASUS ProArt PZ13 (Snapdragon X Plus / Adreno X1-45)에서 확인했어요.

### Steam Deck / Linux (검증됨)

> [!IMPORTANT]
> Steam Deck에서 쓰려면 **v0.3.0 이상**이 필요해요. 이전 버전도 Deck에서 실행은 되지만 F1 메뉴를 조작할 수 없어요.

게임의 네이티브 Linux 빌드와 BepInEx Linux 빌드를 함께 써서 동작해요. `Install.exe`는 Windows용이라 Deck에서는 설치 스크립트를 쓰세요.

Desktop Mode에서 **설치 스크립트(권장)**를 이렇게 실행하세요:

1. [Releases 페이지](https://github.com/TomXV/dragnwash-localization/releases)에서 zip을 받아 압축을 풀어요(우클릭 → Extract).
2. 압축을 푼 폴더를 열고 빈 곳을 우클릭해서 **Open Terminal Here**를 골라요.
3. 아래 명령을 입력하고 Enter를 눌러요:

   ```bash
   bash install-steamdeck.sh
   ```

4. **Install / Update**를 고르고 언어를 선택해요. 실행 옵션을 설정하려면 Steam을 잠깐 꺼야 하는데, 스크립트가 먼저 물어본 다음 나중에 Steam을 다시 실행해 줘요.
5. Gaming Mode로 돌아가서 게임을 실행해요. 언어는 나중에 **Options → Language (Mod)**에서 바꿀 수 있어요.

스크립트는 Steam 라이브러리(SD 카드 포함)에서 게임을 찾고, 공식 Linux BepInEx 5.4.23.5를 받아 SHA-256을 확인해요. 충분히 새로운 Drag'n Wash ModFramework가 없으면 그것도 같은 식으로 받아요. 그다음 `run_bepinex.sh`에 `executable_name="DragNWash"`를 설정하고 모드를 복사한 뒤, 원래 있던 옵션은 그대로 두고 게임 실행 옵션에 `./run_bepinex.sh %command%`를 추가해요. 업데이트나 삭제도 같은 명령을 다시 실행해서 **Install / Update**나 **Uninstall**을 고르면 돼요. 제거할 때 세이브 히스토리는 남겨 두고, 다른 BepInEx 모드가 필요로 하지 않으면 실행 옵션에서 `./run_bepinex.sh`를 빼고 BepInEx도 지울지 물어봐요. `--install`과 `--uninstall`을 붙이면 확인 질문을 건너뛰어요.

Steam은 켜져 있는 동안 실행 옵션을 덮어쓰기 때문에, 실행 옵션을 바꿔야 할 때는 스크립트가 Steam을 끄고 고친 다음 다시 켜요. 기본적으로는 먼저 물어보고, `--close-steam`을 붙이면 묻지 않아요. 어떤 단계가 제대로 안 되면 마지막 대화상자에서 그 사실과 직접 바꿔야 할 항목을 알려 줘요. 실행할 때마다 로그가 `~/.local/state/dragnwash-installer/installer.log`에 남아요.

<details>
<summary>Deck 수동 설치</summary>

1. [BepInEx_linux_x64_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_linux_x64_5.4.23.5.zip)을 게임 폴더(`~/.local/share/Steam/steamapps/common/Drag'n Wash/`)에 풀어요.
2. 이 모드의 `BepInEx/` 폴더를 같은 곳에 합쳐 넣어요. Drag'n Wash ModFramework도 합쳐 넣어요(아래 수동 설치 3단계 참고).
3. `run_bepinex.sh`를 열어 `executable_name="DragNWash"`로 설정하고 저장한 다음 `chmod +x run_bepinex.sh`를 실행해요.
4. Steam 게임 속성 → 실행 옵션에 `./run_bepinex.sh %command%`를 넣어요.
5. 게임을 실행하고 **Options → Language (Mod)**에서 언어를 바꿔요.

</details>

폰트는 따로 설정할 필요 없어요. 게임 텍스트는 SteamOS에 있는 Noto Sans CJK(일본어/중국어/한국어) 폰트 파일을 바로 쓰고, F1 메뉴는 함께 들어 있는 Noto Sans JP(`dragnwash-menufont.bundle`)로 그려요. Steam의 Linux 런타임이 Unity 메뉴 시스템에 CJK 폰트를 주지 않기 때문이에요. 이 메뉴 폰트에는 한글과 히브리어 글리프가 없어서, Deck에서는 F1의 해당 언어 버튼에 로케일 코드가 대신 표시돼요. 게임 본문은 제대로 나와요.

Deck에서 언어를 바꿀 때는 컨트롤러로 **Options → Language (Mod)**를 쓰면 되니까 F1 키는 필요 없어요. Gaming Mode에서 F1 메뉴의 번역 도구를 쓰려면 이렇게 하세요:

- Steam Input으로 버튼 하나에 **F1**을 바인딩해서 메뉴를 열어요.
- 포인터는 오른쪽 트랙패드나 터치스크린으로 움직여요. **A**, **R2**, 트랙패드 클릭으로 포인터 아래 버튼을 누를 수 있어요. Steam Input은 트랙패드 클릭을 마우스 클릭이 아니라 스틱 입력으로 보내기 때문에 모드가 따로 처리해요.
- 창은 제목 표시줄에서 그 버튼을 누른 채 드래그하면 옮겨지고, 오른쪽 아래 모서리에서 똑같이 드래그하면 크기가 바뀌어요.
- 스틱과 D-pad로는 포인터가 올라가 있는 목록을 스크롤해요.

> [!WARNING]
> **지금 macOS에서는 실험적인 방법으로만 동작해요.** Drag'n Wash는 Unity 6.3으로 만들어졌는데, macOS용 BepInEx 5.4.23.5에 들어 있는 Doorstop 로더는 Unity 6.3 게임에 훅을 걸지 못해요([NeighTools/UnityDoorstop#108](https://github.com/NeighTools/UnityDoorstop/issues/108)). Doorstop은 게임에 로드되지만 BepInEx가 시작되지 않아서, `BepInEx/LogOutput.log`와 `BepInEx/config`가 생기지 않고 게임은 영어로 실행돼요. Apple M3 Pro + macOS 26.6에서 네이티브와 Rosetta 둘 다 확인했어요. 이 문제의 수정([NeighTools/UnityDoorstop#117](https://github.com/NeighTools/UnityDoorstop/pull/117))은 아직 UnityDoorstop의 `ci` 프리릴리스에만 들어 있어요. 또 Apple 실리콘에서 네이티브로 실행하면 BepInEx 5.4.23.5가 패치를 적용하지 못하는데, 그 수정([BepInEx/BepInEx#1402](https://github.com/BepInEx/BepInEx/pull/1402))도 아직 릴리스되지 않았어요.
>
> 지금 쓸 수 있는 우회 방법을 설치해 주는 **실험적(experimental)** macOS 설치 스크립트를 저장소에 넣어 두었어요: [`installer/experimental/install-macos.sh`](installer/experimental/install-macos.sh). 릴리스 zip에는 들어 있지 않아요. macOS용 BepInEx 5.4.23.5를 UnityDoorstop `ci` 프리릴리스의 Doorstop(게임에서 테스트한 4.6.0 빌드로 고정)과 함께 설치하고, 게임을 Rosetta(x86_64)로 실행하게 해요. 또 Steam 오버레이를 게임에 넘겨줘서 Shift+Tab으로 열 수 있게 해요. 이 변경은 2026-09-25에 UnityDoorstop에도 머지되어 4.6.0에 들어갔어요([NeighTools/UnityDoorstop#121](https://github.com/NeighTools/UnityDoorstop/pull/121)). 고정한 빌드를 포함해 Doorstop 4.6.0 이상에서는 스크립트가 따로 고칠 필요가 없어서 이 수정을 건너뛰어요. Steam Deck 스크립트처럼 Drag'n Wash ModFramework, 모드, 언어, Steam 실행 옵션도 설정해 주고, 게임을 실행한 뒤 모드가 로드됐는지 확인하는 **Check** 모드도 있어요. `ci` 프리릴리스는 master 브랜치가 바뀔 때마다 같은 URL로 다시 만들어지기 때문에, 거기에 고정한 파일은 바뀌거나 사라질 수 있어요. 전에 고정해 둔 4.5.0 빌드는 2026-09-25에 `ci`가 4.6.0으로 바뀐 뒤로 더 이상 내려받을 수 없어요. 그래서 스크립트는 4.6.0 빌드를, 이 저장소의 프리릴리스에 손대지 않고 올려 둔 복사본([`doorstop-4.6.0-ci-97293a28`](https://github.com/TomXV/dragnwash-localization/releases/tag/doorstop-4.6.0-ci-97293a28))에서 내려받고 SHA-256을 확인해요. UnityDoorstop 4.6.0 안정 릴리스가 나오고 테스트를 마치면 그 릴리스로 바꿀 거예요. 진행 상황은 [TomXV/dragnwash-modframework#85](https://github.com/TomXV/dragnwash-modframework/issues/85)에서 볼 수 있어요. 내려받은 Doorstop이 일치하지 않거나 더 이상 내려받을 수 없으면(HTTP 404), 스크립트는 게임 폴더를 바꾸지 않고 멈추고 이 이슈를 안내해요. 4.6.0 안정 릴리스에는 UnityDoorstop의 수정([NeighTools/UnityDoorstop#117](https://github.com/NeighTools/UnityDoorstop/pull/117)과 [NeighTools/UnityDoorstop#114](https://github.com/NeighTools/UnityDoorstop/pull/114))이 둘 다 들어갈 것으로 예상해요. 둘 다 고정한 빌드에 이미 들어 있기 때문이에요. 그다음에 스크립트가 기다리는 것은 BepInEx의 arm64 수정([BepInEx/BepInEx#1288](https://github.com/BepInEx/BepInEx/pull/1288)과 [BepInEx/BepInEx#1402](https://github.com/BepInEx/BepInEx/pull/1402))이 들어간 BepInEx 릴리스뿐이고, 나오면 그 릴리스로 바꿀 거예요. 그 릴리스에서는 Rosetta 없이 네이티브로 실행될 것으로 예상해요. 다만 아직 테스트하지는 않았어요. 이 조합은 고정한 4.6.0 빌드를 포함해 Apple A18 Pro + macOS 27.2에서 직접 확인했어요. 스크립트는 같은 Mac의 실제 게임에서, 직접 설치해 둔 환경 위에 한 번 실행해 봤어요(이미 설치되어 있어서 아무것도 내려받지 않았어요). 아무것도 없는 상태에서 새로 설치하는 경우는 게임 폴더의 테스트용 복사본에서만 실행해 봤어요. 게임은 Steam에서 실행하세요. 터미널에서 실행하면 세이브를 찾지 못해요. 게임이 시작되는 동안 게임 창이 앞에 있지 않으면 타이틀 화면 전에 멈춘 것처럼 보일 수 있어요. 창을 클릭하면 계속 진행돼요(같은 Mac에서 확인했어요).

### 수동 설치

### 준비물

- Drag'n Wash의 Windows Steam 버전
- [BepInEx 5 for 64-bit Windows (Mono)](https://github.com/BepInEx/BepInEx/releases)
- 이 저장소 [Releases 페이지](https://github.com/TomXV/dragnwash-localization/releases)의 최신 `DragNWashLocalization-<version>.zip`
- Drag'n Wash ModFramework [Releases 페이지](https://github.com/TomXV/dragnwash-modframework/releases)의 `DragNWash.ModFramework-<version>.zip`: 모드 zip의 `mod-install.json`에 있는 `framework.version` 버전 또는 그보다 최신 버전

> [!IMPORTANT]
> 릴리스 자산에서 `DragNWashLocalization-<version>.zip` 파일을 받으세요. GitHub가 자동으로 만드는 **Source code** 아카이브는 설치할 수 있는 모드 패키지가 아니에요. Releases 페이지에 모드 ZIP이 없다면 설치할 수 있는 빌드가 아직 안 나온 거예요.

### 1. 게임 폴더 열기

Steam에서 **Drag'n Wash**를 우클릭하고 **관리 → 로컬 파일 보기**를 고르세요. 게임 `.exe`가 있는 루트 폴더가 열려요.

### 2. BepInEx 설치

**Windows x64 (Mono)**용 BepInEx 5 압축 파일을 받아서 게임 루트에 바로 압축을 풀어요.

압축을 풀고 나면 `winhttp.dll`, `doorstop_config.ini`, `BepInEx` 폴더가 게임 실행 파일 바로 옆에 있어야 해요. 다른 폴더 안에 한 겹 더 들어가 있다면 게임 루트로 옮기세요.

게임을 한 번 실행해서 타이틀 화면까지 들어간 다음 끄세요. 그러면 BepInEx가 설정 파일과 로그 파일을 만들어요. 다음으로 넘어가기 전에 `BepInEx/LogOutput.log`가 생겼는지 확인하세요.

### 3. Drag'n Wash Localization 설치

[Releases](https://github.com/TomXV/dragnwash-localization/releases)에서 `DragNWashLocalization-<version>.zip`을 받아 **같은 게임 루트**에 압축을 풀어요. 압축 프로그램이 안에 든 `BepInEx` 폴더를 합칠지 물으면 허용하세요.

플러그인 DLL은 이 경로에 있어야 해요:

```text
<Drag'n Wash folder>/BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll
```

이 zip에는 모드가 쓰는 Drag'n Wash ModFramework가 들어 있지 않아요. 프레임워크 zip에서 다음 항목도 같은 게임 루트에 복사해 주세요. 폴더 `BepInEx/plugins/DragNWash.ModFramework`, `DragNWash.ModFramework.Text`, `.Dialogue`, `.ToolWindow`, `.Assets`, `.Saves`, 그리고 `BepInEx/patchers/DragNWash.ModFramework.Preloader.dll`이에요. 다른 모드가 이미 더 새로운 ModFramework를 설치했다면 새 파일 쪽을 남겨 두세요.

`plugins`와 DLL 사이에 ZIP 파일 자체나 `DragNWashLocalization-<version>` 같은 폴더가 한 겹 더 끼어 있으면 안 돼요.

### 4. 실행 및 확인

Drag'n Wash를 실행하세요. 수동으로 설치했다면 처음 언어는 일본어예요. 설치 프로그램을 썼다면 거기서 고른 언어로 시작해요.

언어를 바꾸려면 **Options**를 열고 Gameplay 섹션 맨 아래에 있는 **Language (Mod)**를 쓰세요. 언어를 고르면 바로 적용되고, 그대로 쓰려면 **Save**, 저장돼 있던 언어로 돌아가려면 **Back**을 누르면 돼요. 마우스와 게임패드 둘 다 되고, Steam Deck에서도 똑같아요. **F1** 창의 **Translation** 탭에서도 언어를 바꿀 수 있는데, 여기서는 고르는 즉시 저장돼요.

제대로 설치됐다면 `BepInEx/LogOutput.log`에 `DragNWashLocalization` 시작 기록이 남아요.

기본 언어를 직접 바꾸려면 게임을 끄고 아래 파일을 고치세요:

```text
BepInEx/config/com.tomxv.dragnwash.localization.cfg
```

`[General]` 아래의 `TargetLocale`을 `ja`나 `zh-Hans`처럼 설치된 로케일로 바꾼 뒤 게임을 다시 실행하세요. `en`으로 두면 모드는 설치된 채로 번역만 하지 않아서 게임 원래 영어가 그대로 나와요. 같은 설정은 설치 프로그램, Options → Language (Mod), F1 메뉴에서도 고를 수 있어요.

### 모드가 로드되지 않을 때

- BepInEx와 모드를 둘 다 게임 실행 파일이 있는 폴더에 풀었는지 확인하세요.
- DLL이 위에 적은 경로에 정확히 있는지 확인하세요.
- `BepInEx/LogOutput.log`를 열어 보세요. 파일이 없으면 BepInEx부터 로드되지 않은 거예요. 파일이 있으면 `DragNWashLocalization`를 검색해서 그 근처에 에러가 있는지 보세요.
- Options를 열 때 Direct3D 12 크래시가 난다면 [Windows에서 Options 열 때 크래시](#windows에서-options-열-때-크래시)에 적힌 해결 방법을 따라 하세요.
- 예전에 게임 파일을 덮어쓰는 번역(예: `DragNWash_Data`에 파일 복사)을 설치한 적이 있다면, 게임의 영어 원문이 이미 없어져서 모드가 번역할 대상을 찾지 못해요. 먼저 Steam에서 게임을 우클릭 → **속성** → **설치된 파일** → **게임 파일 무결성 확인**으로 원본 파일을 되돌린 다음, 모드를 다시 설치하세요. 이렇게 덮어쓰는 번역은 게임이 업데이트되면 깨지거나 아예 안 돌아가지만, 이 모드는 게임 파일을 건드리지 않아요.

## Language packs

번역 파일은 TomXV가 만들어 같은 zip에 넣었고, 팩을 다듬어 주신 분들은 아래 표의 해당 줄에 이름을 올려요([기여자 크레딧](CONTRIBUTING.md#credits-for-contributors) 참고). 설치 프로그램과 F1 메뉴에는 `Translations/` 아래 폴더가 전부 나와요.

| Locale | Language | Status |
| --- | --- | --- |
| `ja` | 日本語 | 제작자 감수 |
| `zh-Hans` | 简体中文 | 제작자 감수 |
| `zh-Hant` | 繁體中文 | 임시, 감수된 간체 중국어를 대만식 표현으로 변환 |
| `de` | Deutsch | 임시 |
| `fr` | Français | 임시 |
| `es` | Español | 임시 |
| `pt-BR` | Português (Brasil) | 임시 |
| `ko` | 한국어 | 원어민 교정 완료, Hotcake (게임에서 쓰이지 않는 줄은 그대로 둠) |
| `ru` | Русский | 임시 |
| `pl` | Polski | 임시 |
| `he` | עברית | 임시, 우->좌 표시 |
| `uk` | Українська | 임시 |
| `th` | ไทย | 임시 (단어 사이의 폭 없는 공백으로 줄바꿈) |
| `vi` | Tiếng Việt | 임시 |
| `tr` | Türkçe | 원어민 번역, [DarkBloodKing](https://github.com/Darkbloodking00) |
| `eo` | Esperanto | 임시, 재미용 |
| `tok` | toki pona | 임시, 재미용 (137개 단어 언어라 의역이 많을 수 있음) |
| `en` | English | 게임 원본 텍스트 (번역 없음) |

> [!NOTE]
> **임시(Provisional)** 팩은 원어민이 검수하지 않았어요. 처음부터 끝까지 플레이할 수는 있지만 어색한 문장이 있거나 농담이 빠졌을 수도 있어요. 제작자가 직접 감수한 건 일본어와 간체 중국어뿐이에요. 원어민이라면 PR로 고쳐 주시면 정말 고맙겠어요([CONTRIBUTING.md](CONTRIBUTING.md) 참고). 같은 안내가 모든 `strings.csv` 맨 위에도 적혀 있어요.

## 번역 기여자를 위한 안내

번역은 `Translations/<locale>/strings.csv`를 고쳐서 추가해요. 배포 파일의 컬럼은 `key,section,node,order,speaker,translation`이에요. `key`는 영어 원문의 해시이고, `section`/`node`/`order`는 게임 안에서 나오는 위치(레벨/대화/순서), `speaker`는 말하는 캐릭터예요. `#`로 시작하는 줄은 `# ===== Level 1: Ryan (Sunny) =====` 같은 섹션 헤더라서, 파일을 위에서부터 읽으면 대본처럼 읽혀요. 추천하는 작업 순서는 이래요.

1. 게임에서 **F1 → Translation → Export working copy**를 실행해요. 그러면 `Translations/_discovered/<locale>.working.csv`가 만들어지는데, 각 줄 옆에 영어 원문이 같이 들어 있어요(`key,section,node,order,speaker,source_en,translation`). 줄 순서와 섹션 헤더는 똑같아요.
2. `translation` 컬럼을 고쳐요. 저장하면 실행 중인 게임에 바로 핫리로드돼요.
3. 커밋하기 전에 **F1 → Translation → Hash for commit**(또는 `tools/hash-strings.ps1`)을 실행해요. 그러면 영어 원문을 뺀 `strings.csv`가 다시 만들어져요.

언어 폴더마다 언어 표시 이름(예: `日本語`) 한 줄만 적힌 `name.txt`도 있는데, 이 이름이 설치 프로그램과 게임 안 메뉴에 나와요.

게임의 영어 스크립트 원문은 일부러 이 저장소에 넣지 않아요. 그래야 **게임을 정식으로 가진 사람만 번역을 만들 수 있기** 때문이에요. Unity 내부 키를 알 필요도, 코드를 짤 필요도 없어요. 자세한 건 [CONTRIBUTING.md](CONTRIBUTING.md)를 보세요.

원문에 `<size=70%>` 같은 서식 태그가 있으면 태그 구조는 그대로 두고 안쪽 글자만 번역하세요.

### 맥락 확인용 전체 대사 내보내기

이 기능들은 개발자 도구예요. 플레이어용으로는 꺼져 있어서 그대로는 아래 기능이 동작하지 않으니, 먼저 **Options → Mods → Drag'n Wash ModFramework → Developer tools**를 켜세요. 그다음 세이브를 불러와서 게임에서 **F6**을 누르면 모든 대사가 여기로 내보내져요:

`BepInEx/plugins/DragNWashLocalization/Translations/_discovered/dialogue_lines.csv`

실제로 설치한 게임에서 1,839줄로 확인했어요.

줄은 게임에서 나오는 순서대로 정렬돼요. `node` 컬럼은 대화마다 붙는 이름으로, `Alexander_2_intro`처럼 생겼어요(패턴: "character name_occurrence_scene"). `order` 컬럼은 그 대화 안에서 몇 번째 줄인지를 나타내요. `kind` 컬럼은 캐릭터 대사(`line`)인지 플레이어 선택지(`option`)인지를 구분해요. 덕분에 누가 말하는지, 답변이 뭘 가리키는지 알기 쉬워요. 게임에 나오는 세 드래곤은 Conrad, Ryan, Alexander예요.

번역할 줄을 `Translations/<locale>/strings.csv`로 복사해서 `translation` 컬럼을 채우고 게임에서 확인해 보세요. `node`, `key` 같은 컬럼이 더 남아 있어도 플러그인은 파일을 제대로 읽어요.

**PR을 열기 전에 공개용 파일을 다시 만드세요.** 게임 안의 **Hash for commit** 버튼(또는 `tools/hash-strings.ps1`)을 쓰면 돼요. 자동 검사는 공개용 헤더만 통과시켜요. *Hash for commit*이 쓰는 `key,section,node,order,speaker,translation`, 또는 더 짧은 `key,speaker,translation`과 `key,translation`이에요. `source_en` 같은 내보내기용 컬럼이 남은 파일은 거부되는데, 영어 원문이 들어간 PR이야말로 이 저장소가 가장 피하고 싶은 거예요. 자세한 건 [CONTRIBUTING.md](CONTRIBUTING.md#hash-before-committing)를 보세요.

이미 번역한 줄은 다시 내보낼 때 번역이 채워진 채로 나오니까, 다시 내보내도 해 둔 작업이 날아가지 않아요.

### 게임 재시작 없이 수정 미리보기

게임을 켜 둔 채로 `Translations/<current-language>/strings.csv`나 작업본 `_discovered/<locale>.working.csv`를 저장하면, 2초쯤 뒤에 플러그인이 알아서 다시 읽어서 지금 화면에 보이는 텍스트를 바로 바꿔 줘요. 바뀐 줄은 하나하나 F1 Activity 로그에 남아요.

그래서 게임을 다시 시작하지 않고도 번역을 고치고 확인하는 걸 몇 번이고 반복할 수 있어요. 이 기능은 `[Debug] HotReloadTranslations`로 끌 수 있어요. 다시 읽기에 성공하면 F1 Activity 로그에 `[reload]` 항목이 떠요.

### 모든 UI 텍스트 내보내기

**F7**을 누르면 로드된 UI 텍스트를 전부 여기로 내보내요:

`Translations/_discovered/ui_texts.csv`

**숨겨진 메뉴**도 같이 내보내기 때문에, 일시정지 메뉴나 확인 대화상자를 일일이 열지 않아도 지금 씬의 UI 문자열을 거의 다 모을 수 있어요. 컬럼은 `key`, `source_en`, `translation`(이미 번역이 있으면), `object_path`(UI 안에서의 위치)예요.

타이틀 화면에서 한 번, 플레이 중에 한 번 F7을 누르면 UI 텍스트는 거의 다 모여요.

아직 번역 안 된 UI 텍스트는 플레이하는 동안 `Translations/_discovered/strings.csv`에도 자동으로 쌓여요. 이 파일은 게임을 시작할 때마다 번역이 끝난 항목과 중복을 걸러 내서, 남은 할 일만 최신 상태로 보여 줘요.

### 번역이 필요 없는 문자열

슬라이더 값이나 `1920 x 1080 @ 164.995Hz` 같은 해상도 표기, 빌드 번호 같은 건 기본적으로 모으지 않아요.

제외할 패턴은 [Translations/ignore.txt](Translations/ignore.txt)에 추가하면 돼요. 정규식으로 쓰고, 파일 안에 예시도 있어요.

제외는 "수집(discovery)"에만 적용돼요. 번역을 먼저 찾아보기 때문에, `strings.csv`에 있는 항목은 제외 패턴에 걸려도 항상 번역돼요.

### 인게임 디버그 메뉴

**F1**을 누르면 도구 창이 열리고 닫혀요. 이 창은 Drag'n Wash ModFramework를 쓰는 다른 모드와 같이 써요. 키는 `BepInEx/config/com.tomxv.dragnwash.modframework.toolwindow.cfg`의 `[General] ToggleKey`에서 정해요. 제목 표시줄을 드래그하면 옮겨지고, 오른쪽 아래를 드래그하면 크기가 바뀌어요. 이 모드는 탭 4개를 추가해요.

- **Activity log:** 번역 결과와 처리 로그가 나와요. `Follow: ON/OFF`로 최신 항목까지 자동으로 스크롤할지 정하고, 직접 스크롤하면 follow가 꺼져요. `Clear log`는 화면을 비우고 중복 억제도 초기화해요. 로그는 최근 100개까지 남아요.
- **Translation:** 재시작 없이 언어를 바꿀 수 있어요. 버튼에는 `name.txt`의 언어 이름이 나오고, **English**를 누르면 번역이 꺼져요. 여기서 대사 내보내기(`Export loaded dialogue`), UI 텍스트 내보내기(`Export UI text`), 영어가 같이 적힌 작업본 내보내기(`Export working copy`), 배포 파일 다시 만들기(`Hash for commit`), 레이아웃 점검도 할 수 있어요.
- **Saves:** 이전 세이브를 되돌리거나, 레벨 인덱스를 올리고 내리거나, 세이브 플래그를 켜고 끌 수 있어요. 자세한 건 아래에 있어요.
- **About:** 지금 실행 중인 모드의 버전/빌드, 제작자, 라이선스, 이번 세션의 로드 정보가 나와요. 버그 리포트에 그대로 옮겨 적기 좋아요.

**Check translation layout** 버튼은 레이아웃 밖으로 넘칠 것 같은 문자열을 `Translations/_discovered/layout_risks.csv`로 내보내요. 기준값은 `BepInEx/config/.../LayoutOverflowThreshold`에서 정하고, 기본값 `1.0`은 딱 맞는 크기를 뜻해요.

### 번역 테스트용 이전 세이브 복원

게임이 세이브를 저장할 때마다 플러그인이 버전별 복사본을 여기에 남겨요:

`BepInEx/SaveHistory/<slot>/`

기본으로 슬롯마다 30개 버전을 남기고, 개수는 `BepInEx/config/com.tomxv.dragnwash.modframework.saves.cfg`의 `[History] Keep`이나 Mods 화면에서 바꿀 수 있어요. 예전 버전 모드가 `BepInEx/plugins/DragNWashLocalization/SaveHistory`에 두던 복사본은 처음 실행할 때 이 위치로 옮겨져요.

**F1 → Saves**를 열고 슬롯을 고른 뒤 원하는 버전의 **Restore**를 누르세요. 그다음 타이틀 화면으로 돌아가 그 슬롯을 불러오면 되돌린 세이브가 반영돼요. 그 뒤로 게임에서 저장하면 평소처럼 지금 쓰는 세이브를 덮어써요.

되돌리기 직전 상태도 따로 자동 보관되니까, 너무 많이 되돌렸어도 다시 돌아올 수 있어요.

이걸로 같은 장면을 여러 번 다시 보면서 번역을 고친 버전끼리 비교하기 쉬워요. Restore는 게임 세이브 파일 자체를 바꾸지만, 플래그나 변수를 직접 고치지는 않아요.

같은 탭에는 **PROGRESS** 편집기도 있어요. **-** / **+**로 레벨 인덱스를 옮기고 **Apply**를 누르세요. 앞으로 넘길 때는 아직 안 본 내용의 스포일러가 될 수 있어서 한 번 더 물어봐요. **Flags...**는 게임이 쓰는 걸로 알려진 이벤트 플래그를 전부 그룹별(레벨 진행, 스토리, 연애, 씬 트리거, 씬 감상 여부, 세척 세션, 아이템, 디버그)로 보여 주고, 항목마다 짧은 설명과 지금 세이브에서 설정돼 있는지를 표시해요. 값을 클릭하면 unset → true → false 순서로 바뀌고, 검색창에 입력하면 걸러지고, **Reset all to false...**로 모든 플래그를 false로 돌릴 수 있어요(레벨 인덱스는 그대로). 이 목록은 플러그인 DLL 옆의 `FlagCatalog.csv`에서 읽어 오기 때문에, 나중에 찾은 플래그는 줄만 추가하면 반영돼요. 무엇을 고치든 그 전에 세이브 스냅샷을 먼저 만들어요.

## Windows에서 Options 열 때 크래시

Unity 6000.3.14f1 + DirectX 12에서 Options를 열 때 `D3D12ScratchAllocator::DestroyScratch` 크래시가 나는 걸 확인했어요. Unity 공식 이슈 트래커에도 같은 스택 트레이스가 올라와 있어요: [UUM-140564](https://issuetracker.unity.com/issues/10698). 네이티브 렌더링 버그라서 번역 훅에서 예외를 잡아도 막을 수 없어요.

이 버그는 실행 중에 텍스처를 할당하거나 업로드할 때 터져요. 그래서 Direct3D 12에서는 플러그인이 시작할 때 설치된 모든 언어의 폰트를 미리 준비해 둬요. 그러면 플레이 중이나 Options/F1에서 언어를 바꿀 때 폰트 아틀라스에 새 항목이 추가되지 않아요. 글자마다 그 언어가 쓰는 폰트 하나에만 래스터라이즈하니까 시작할 때 부담도 크지 않아요. 실제 게임에서 언어를 여러 번 바꿔 보는 것까지 확인했어요. 따로 설정할 건 없어요.

다른 그래픽 API(Direct3D 11, Steam Deck의 Vulkan)에서는 실행 중에 업로드해도 괜찮아서, 지금 언어만 먼저 준비하고 나머지는 고를 때 로드해요. 여기서도 시작할 때 전부 준비해 두고 싶다면 `[Font] PreloadAllLocales = true`로 설정하세요.

그래도 크래시한다면 Steam의 **Drag'n Wash → 속성 → 일반 → 실행 옵션**에 `-force-d3d11`을 넣고 다시 시작하세요. 그래픽 API를 바꿔서 문제를 피해 가는 방법이에요. 이 옵션은 [Unity 표준 명령행 인자](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html) 중 하나이고, 게임 DLL이나 세이브 데이터는 건드리지 않아요.

지금 쓰는 그래픽 API는 `BepInEx/LogOutput.log`의 플러그인 시작 기록에 `graphics=...` 형태로 남아요.

`BepInEx/config/com.tomxv.dragnwash.modframework.assets.cfg`의 `[Fonts] AtlasPointSize`를 낮추면 폰트 아틀라스 수가 줄고, 높이면 글자가 더 선명해져요. 기본값은 80이에요.

## Exclusive 전체 화면에서 창 전환 후 멈춤 (Windows)

DirectX 12에서 **Window Mode**를 **Exclusive**로 둔 채 다른 창으로 전환했다가(Alt+Tab, 또는 다른 창 클릭) 돌아오면, 게임이 멈췄다가 크래시할 수 있어요. 크래시 보고서를 보면 Windows가 게임을 전용 전체 화면에서 빼거나 다시 넣는 동안 Unity의 DirectX 12 스왑 체인이 멈춰 있어요. `D3D12SwapChain::Present`가 `887a0001`로 실패하고, 로그에는 그 전에 `D3D12Fence::Wait ... May cause crash`가 자주 찍혀 있어요. 그 순간에는 모드 코드가 돌고 있지 않아서, 이 모드가 아니라 게임 그래픽 코드의 문제예요.

피하려면 둘 중 하나를 해 보세요.

- Steam에서 **Drag'n Wash → 속성 → 일반 → 실행 옵션**에 `-force-d3d11`을 추가해요. 이 옵션을 쓰면 Exclusive 전체 화면에서 창을 전환해도 멈추지 않는 걸 확인했어요.
- **Window Mode**를 **Exclusive** 대신 **Fullscreen**으로 둬요. 창을 전환할 때 디스플레이 모드까지 바꾸는 건 전용 전체 화면뿐이라 이걸로도 피할 수 있을 거예요. 이쪽은 아직 확인하지 못했어요.

## 현재 상태

앞으로의 계획은 [docs/ROADMAP.md](docs/ROADMAP.md)(영어)에 있어요.

최신 릴리스는 v1.3.0이에요. Direct3D 12에서 크래시가 줄었고, 게임이 멈추면 무슨 일이 있었는지 창으로 알려 줘요(Drag'n Wash ModFramework 1.3.0 포함). v1.2.1에서는 2026년 9월 14일 게임 업데이트 뒤에 만든 세이브가 Saves 탭에 다시 나오고, 번역자 작업 사본에 열려 있지 않던 화면의 영어도 채워지게 했어요. v1.2.0에서는 우크라이나어·태국어·베트남어를 더해 16개 언어가 됐고, 한국어 원어민 교정, 게임 업데이트에도 사라지지 않는 번역, 로고, Drag'n Wash ModFramework 1.2.0이 들어갔어요. v1.1.2에는 직접 그린 Mods 화면 아이콘이 들어간 Drag'n Wash ModFramework 1.1.2가 포함돼 있어요. v1.1.1에는 프레임워크의 첫 아이콘이 들어간 ModFramework 1.1.1이 들어 있었어요. v1.1.0부터는 이 모드의 새 릴리스가 나오면 Mods 화면과 타이틀 화면에서 알려 줘요(ModFramework 1.1.0과 함께). v1.0.0에서 [Drag'n Wash ModFramework](https://github.com/TomXV/dragnwash-modframework) 위에서 동작하도록 바뀌었고 Mods 화면이 생겼어요. v0.6.2에서는 게임 업데이트 전의 작업본으로 "Hash for commit"을 실행하면 줄이 빠지던 문제를 막고 "Really Delete Save?"를 번역했어요. v0.6.1에서는 히브리어에서 이름과 일부 미번역 텍스트가 거꾸로 나오던 문제를 고쳤어요. v0.6.0에서는 같은 영어 문장도 화자마다 다르게 번역할 수 있는 줄 단위 번역을 넣었고, 2026-09-14 게임 업데이트 기준으로 확인했어요. v0.5.0에서는 게임의 Options 화면에서 언어를 바꿀 수 있게 됐고, v0.4.0에서는 13개 언어, 언어별 폰트, About 탭, 영/일/중 설치 프로그램이 들어갔어요. v0.3.0에서 Steam Deck을 지원하기 시작했어요. Windows on ARM에서도 확인했고(이 환경에서는 게임 자체에 `-force-d3d11` 필요), macOS는 BepInEx 쪽의 알려진 문제 때문에 지금은 실험적인 방법으로만 동작해요([Steam Deck / Linux](#steam-deck--linux-검증됨) 항목의 주석 참고). BepInEx 플러그인 뼈대, UI와 대사의 일본어/중국어 번역 치환, CJK 폰트 렌더링, 대사/UI 일괄 내보내기, 인게임 디버그 메뉴, 레이아웃 오버플로 감지, 번역자용 문서, 릴리스 워크플로까지 모두 만들었고 실제 게임에서 테스트도 마쳤어요.

자세한 내용은 [docs/PLAN.md](docs/PLAN.md)를 보세요.

## Drag'n Wash ModFramework

v1.0.0부터 이 모드는 **Drag'n Wash ModFramework** 위에서 동작해요. 다른 Drag'n Wash 모드도 이 프레임워크로 만들 수 있어요.

- 게임에 훅을 거는 과정에서 이 모드가 만든 기능 중 다른 모드에도 쓸모 있는 부분을 프레임워크로 떼어 냈어요. 설치된 모든 모드의 설정과 켜기/끄기를 보여 주는 **Mods** 화면(Options → Mods), 게임 Options의 언어 행, 텍스트가 표시되기 전의 재작성, 대사/선택지 이벤트, 공유 F1 도구 창, Direct3D 12에서 안전한 폰트, 세이브 히스토리가 여기에 들어가요.
- 이렇게 나눠 두면 게임이 업데이트돼도 프레임워크만 변경을 따라가면 되고, 그 위의 모드는 계속 동작할 수 있어요. 2026-09-14 업데이트가 딱 이런 종류의 변경이었어요.
- 플레이어용 릴리스 zip에는 프레임워크가 들어 있지 않아요. 게임 폴더에 충분히 새로운 버전이 없으면, 설치 프로그램이 이 모드를 빌드할 때 쓴 버전을 GitHub에서 받아 와요. v1.1.0부터 설치 프로그램은 모든 Drag'n Wash 모드가 같이 쓸 수 있는 프레임워크의 공용 설치 프로그램이에요. 이 모드를 지워도 다른 모드가 설치돼 있으면 프레임워크는 남아요.
- v1.1.0부터는 이 모드나 프레임워크의 새 릴리스가 나오면 타이틀 화면에 **1 update available in Mods**가 뜨고, **Options → Mods**에 릴리스 페이지로 가는 버튼이 생겨요. 프레임워크는 하루에 한 번 GitHub에 최신 릴리스를 물어보기만 하고, 사용자나 게임에 대한 정보는 보내지 않으며 아무것도 내려받지 않아요. 이 알림은 **Mods → Drag'n Wash ModFramework → Settings → Check for updates**에서 끌 수 있어요.
- 번역하는 분들에게는 CSV 형식과 번역 도구가 바뀌지 않으니, 기존 팩과 기여한 내용을 그대로 이어 갈 수 있어요.

Drag'n Wash 모드를 만들고 있는데 프레임워크에 있었으면 하는 기능이 있다면 이슈를 열어 주세요.

## 번역 기여

코드는 몰라도 돼요. `Translations/<locale>/strings.csv`를 고치기만 하면 번역에 참여할 수 있어요.

작업 순서, 파일 형식, 아직 번역 안 된 문자열 찾는 법은 [CONTRIBUTING.md](CONTRIBUTING.md)에 있어요.

참여하시는 분은 모두 [행동 강령](CODE_OF_CONDUCT.md)을 따르게 돼요. 보안 문제를 발견했다면 이슈 말고 비공개로 알려 주세요([SECURITY.md](SECURITY.md)). 마음과 여유가 있다면 [GitHub Sponsors](https://github.com/sponsors/TomXV)도 있어요. 어느 쪽이든 모드는 계속 무료이고, 번역해 주시는 게 훨씬 큰 도움이 돼요.

## 배포와 릴리스

릴리스 ZIP을 빌드하고 배포하는 방법은 [docs/RELEASING.md](docs/RELEASING.md)에 있어요.

게임에서 나온 레퍼런스 어셈블리는 여기에 커밋할 수 없어서, ZIP은 GitHub Actions의 **Build** 워크플로가 비공개 저장소에서 그걸 읽어 와 빌드해요. `v*` 태그를 푸시하면 ZIP이 붙은 초안 릴리스가 만들어지고, 노트를 써서 공개하는 건 사람이 해요.

## 개발팀에 드리는 안내

이 프로젝트는 비공식 팬 프로젝트로, Gator Dragon Games와는 제휴 관계가 없습니다. Drag'n Wash ModFramework의 [콘텐츠 정책](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.md)에 따라 게임 에셋이나 스크립트 원문을 그대로 넣지 않고, 영어 문장은 SHA-256 해시로만 저장하며, 게임 파일 자체는 건드리지 않습니다(플러그인은 BepInEx가 실행 중에 불러옵니다). 개발팀 분들께서 우려되는 점이 있으시면 이 저장소에 이슈를 열거나 유지보수자에게 연락해 주세요. 요청에 맞춰 프로젝트를 고치거나 내리겠습니다.

## 크레딧

- 이 모드의 **로고**(모드 화면의 아이콘, `icon.png`)는 **Mister ERIO**([@mistererio](https://github.com/mistererio))가 그렸고, 허락을 받아 쓰고 있어요.
- Drag'n Wash ModFramework에 들어 있는 Options 화면의 **Mods 버튼**도 Mister ERIO의 작품이에요.
- Drag'n Wash ModFramework의 **로고와 아이콘**(아이콘은 프레임워크에 포함)은 **NotaGames**([@NotaGames](https://github.com/NotaGames))의 작품이에요.
- 한국어 팩은 **Hotcake**가 교정해 주었어요.
- 언어 팩을 다듬어 주신 번역자분들은 [Language packs](#language-packs) 표에 적혀 있어요.

## 라이선스

플러그인 코드와 번역은 MIT 라이선스예요([LICENSE](LICENSE)). 번역은 각 번역자가 쓴 것이고, 이름은 언어 팩 표와 크레딧에 적어 두었어요. 크레딧에 적힌 그림은 그린 분의 것이라 이 라이선스에 포함되지 않아요. 이 저장소에는 게임 에셋이나 코드가 들어 있지 않아요.

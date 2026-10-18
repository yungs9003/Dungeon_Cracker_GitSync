# Shop 화면·영수증·구매

2026-10-18 구현. 시안 원본은 `Reference/Shop.svg`다.

## 화면 구조

- 씬: `Assets/Scenes/Shop.unity`.
- 페이지: `Assets/Prefabs/UI/Shop/ShopPage.prefab`.
- 상품/영수증 단락: 같은 폴더의 `ShopItemCard.prefab`, `ShopReceiptRow.prefab`.
- 코드: `Assets/Scripts/App/UI/Shop/`.
- 재구성: `Tools > Dungeon > Rebuild Shop Scene UI`. 기존 Shop 전용 화면을 다시 구성하므로 프리팹/씬의 수동 배치 변경은 먼저 보관한다. 다른 씬과 공통 메뉴/상태창 프리팹은 재구성하지 않는다.

SVG의640×360 좌표를1920×1080 기준으로 구성했다. Guild와 같은 `GuildReferenceLayout`으로 본문 비율을 유지하고 기존 하단 메뉴 위에 표시한다. 왼쪽 직원은 Shop 이미지, 코멘트 박스/다음 표시는 Guild 이미지를 사용한다. 코멘트는 `ShopPageUI > Comments` 배열을 클릭할 때마다 순환한다.

가운데는 DB의 `items` 배열 순서대로 유효한 아이템6개를3×2로 배치한다. 이름과 가격은 `ItemDefinition.name/gold`, 아이콘은 `Resources/UI/ItemsIcons/{id}`에서 가져온다. 다중 Sprite로 가져온 PNG도 지원한다. 아이콘·상점 원본 PNG는 수정하지 않는다.

우측 영수증은 코드/이름을 첫 줄, 수량/해당 종류 소계를 둘째 줄로 묶는다. 종류가 늘면 스크롤바 없는 목록 안에서 스크롤되며 합계·결제는 고정되어 있다. 가운데 하단 흰색 Wallet 상자에 실제 `StatusSaveFile.Data.gold`를 표시한다.

## 클릭

- 왼쪽 한 번:0.3초 동안 두 번째 클릭을 기다린 뒤 기존 `ItemEquipmentInfoPopupUI`를 열어 이름/등급/분류/설명/아이콘을 표시한다.
- 왼쪽 두 번: 팝업 예약을 취소하고 해당 종류 수량을1 늘린다. 이미 영수증에 있으면 같은 단락을 갱신한다. 네 번 연속 누르면 두 쌍으로 계산해2개만 추가한다.
- 오른쪽: 팝업 예약을 취소하고 수량을1 줄인다.0이 되면 단락을 제거하며 없는 항목을 누르면 그대로 유지한다.
- 결제: 성공하면 영수증을 비우고 보유 골드를 갱신한다. 실패하면 영수증과 구매 전 골드/보유 수량을 유지하며 안내 문구를 표시한다.

빈 영수증·잔액 부족·서비스 없음·결제 중에는 결제 버튼이 회색/비활성화된다. 골드와 합계가 정확히 같으면 구매할 수 있다. 외부 골드 변경은 `OnSaveDataChanged`로 반영된다. 장바구니는 현재 Shop 화면의 임시 선택이며 저장 파일에 별도 보관하지 않는다.

## 데이터·저장 연결

`GameDatabase.ItemDefinition`에 `gold`를 추가했다. 가격 미지정 기본값은-1이며 구매를 허용하지 않는다. 기존 DB JSON의6개 가격은 변경하지 않았다. 기존 아이콘 경로에 없으면 `UI/ItemsIcons`를 사용하는 호환 조회도 추가하여 팝업과 상점에서 같은 아이콘을 사용할 수 있다.

결제는 `StatusSaveFile.TryPurchaseItems(GameDatabase, IReadOnlyDictionary<string,int>, out string)`를 사용한다. 이 메서드는 DB에서 아이템과 가격을 다시 확인하고 잔액·수량·정수 한도를 검사한다. 골드 차감과 `inventory.item` 수량 증가를 같은 Save 한 번으로 확정한다. raw id(`000`)와 item key(`item:000`)를 받아 raw id로 저장하며 기존 중복 아이템 수량도 합산한다. 퀵슬롯과 장비/모듈 데이터는 보존한다.

저장에 실패하면 골드와 아이템 목록을 원본으로 돌린다. 저장 성공 알림 중 재진입 결제도 막는다. 기존 `IStatusSaveStorage`의 원자적 파일 저장·백업·Editor/Player 경로를 그대로 사용한다. 개별 `SetGold`/아이템 지급을 따로 호출하여 중간 상태를 저장하지 않는다.

## 검증 자료

원본 소스6개 어셈블리를 컴파일했다. 분리 Play Mode에서 실제 UI 레이캐스트/클릭과 메모리 저장소, 작업 폴더 안의 전용 `FileStatusSaveStorage` 재로드를 검증한다. 사용자 저장 파일을 실행 데이터로 사용하지 않는다.

최종 Play Mode 검사37개를 통과했다. 실제6개 가격의 글자 렌더, 단일/더블/우클릭·네 번 연속 클릭, 팝업과 아이콘, 영수증 수량/단락 제거/합계, 잔액 부족·정확한 잔액, 외부 골드 변경, 한 번 저장/알림, 저장 실패 롤백/재시도, 기존 수량 합산·퀵슬롯 보존, 잘못된 코드/수량/가격·정수 한도·재진입 결제 차단, 임시 파일 저장 후 재로드를 확인했다.1280×720/1920×1080/2560×1080 렌더도 확인했다.

기준 해시·Before/After·변경 manifest·씬 보존 비교·컴파일/Unity 로그·검사 목록·실제 PNG는 `Docs/Rollback/2026-10-18-shop-ui`에 보관한다. 임시 제작·검증 도구는 `Temp/ShopUI`다. `shop-cart.png`, `shop-item-popup.png`, `shop-insufficient.png`, `shop-receipt-scrolled.png`에서 주요 상태를 확인할 수 있다.

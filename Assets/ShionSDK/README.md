# Shion SDK cho Unity

## Mục đích

`ShionSDK` cung cấp bộ công cụ trong Unity Editor để:
- Quản lý các module SDK trong dự án.
- Cài/gỡ/cập nhật module theo dependency.
- Hỗ trợ cài nhanh các package phổ biến.
- Quản lý AdMob Mediation adapters.
- Quản lý AppLovin Mediation adapters.
- Kiểm tra tương thích phiên bản giữa AdMob và AppLovin, hỗ trợ downgrade và cập nhật XML để tránh xung đột resolver.

## Yêu cầu

- Unity Editor (khuyến nghị dùng bản đang sử dụng cho dự án hiện tại).
- Internet để lấy version/release metadata.
- Các package liên quan tới mediation (nếu sử dụng AppLovin/AdMob).

## Cấu trúc chức năng chính

- `Shion/SDK Manager`: Quản lý module SDK tổng quát.
- `Shion/AdMob Adapters`: Quản lý adapter của AdMob mediation.
- `Shion/AppLovin Adapters` (hoặc cửa sổ AppLovin tương ứng trong SDK): Quản lý adapter của AppLovin mediation.

## Các package/tool hỗ trợ

Thông qua `Shion/SDK Manager`, SDK hỗ trợ cài đặt các package tiện ích thường dùng, bao gồm:
- Các package nội bộ hoặc package bên thứ ba đã được cấu hình trong danh sách module của SDK

## Hướng dẫn nhanh

### 1) Mở SDK Manager

- Vào menu `Shion/SDK Manager`.
- Chọn module cần cài/gỡ.
- Nếu module có nhiều version, chọn version ở cột version.
- Bấm `Install` hoặc `Uninstall`.
- Với nhóm package tiện ích, thao tác cài/gỡ thực hiện tương tự như các module khác.

### 2) Mở AdMob Adapters

- Vào menu `Shion/AdMob Adapters`.
- Cửa sổ này chỉ hiển thị khi Google Mobile Ads đã được cài.
- Chọn adapter và version cần cài, sau đó bấm `Install`.

### 3) Mở AppLovin Mediation Adapters

- Vào menu AppLovin tương ứng do SDK cung cấp.
- Chọn network adapter cần cài/gỡ và version mong muốn.
- Bấm `Install` hoặc `Uninstall`.
- Sau khi cài xong, nên chạy resolve dependencies để đồng bộ project.

## Flow cài đặt adapter (AdMob Adapters)

Khi bấm `Install`, hệ thống sẽ đi theo thứ tự:

1. Resolve version để cài:
   - Thử tìm version đúng hoặc gần nhất theo AppLovin counterpart.
   - Ưu tiên version phù hợp và không vượt quá counterpart nếu có thể.
2. Nếu version được điều chỉnh:
   - Hiện dialog `Version Compatibility Adjustment`.
   - Bạn có thể chọn:
     - `Use compatible version`
     - `Install selected anyway`
     - `Cancel`
3. Nếu tiếp tục theo flow compatible:
   - Kiểm tra support version Android/iOS.
   - Nếu lệch tương thích với AppLovin counterpart, hiện dialog sửa XML.
   - Nếu đồng ý sửa XML, hệ thống update XML theo hướng downgrade (không nâng version).
4. Bắt đầu import/cài adapter.

## Flow cài đặt adapter (AppLovin Mediation)

Khi cài adapter phía AppLovin, hệ thống thực hiện theo nguyên tắc:

1. Cài đúng version đã chọn hoặc version tương thích theo metadata.
2. Đối chiếu tương thích với phía AdMob/AppLovin plugin khi cần.
3. Nếu có lệch support Android/iOS gây rủi ro resolver, có thể cần cập nhật XML theo hướng an toàn.
4. Ưu tiên giữ hệ thống dependency ổn định, tránh nâng version XML không cần thiết.

## Quy tắc XML compatibility

- Mục tiêu là giảm xung đột resolver.
- Khi sửa XML, hệ thống ưu tiên target theo hướng an toàn:
  - Downgrade support version về mức phù hợp hơn.
  - Không chủ động nâng version lên cao hơn counterpart.
- Với AppLovin adapter:
  - Ưu tiên cập nhật XML phía AppLovin adapter của AdMob.
- Với adapter khác:
  - Ưu tiên cập nhật XML phía AppLovin counterpart adapter.
- Với AppLovin Mediation adapter:
  - Áp dụng cùng nguyên tắc tương thích Android/iOS support version khi đối chiếu với adapter counterpart.
  - Ưu tiên cập nhật theo hướng downgrade khi cần sửa XML.

## Lưu ý vận hành

- Sau khi cài/gỡ adapter, có thể cần chạy lại dependency resolve trong dự án.
- Nếu thay đổi XML dependency, nên `Refresh` trong cửa sổ hoặc mở lại window để đồng bộ UI.
- Nếu có domain reload, trạng thái cài đặt sẽ được đối chiếu lại với tình trạng thực tế của project.

## Xử lý sự cố thường gặp

### Sự cố cài đặt package/module

- Cài đặt package thất bại:
  - Kiểm tra kết nối mạng và quyền truy cập nguồn package.
  - Kiểm tra package/version có tồn tại trong danh sách module của SDK.
  - Có thể version hiện tại không hỗ trợ cài đặt qua `UPM`, hãy thử cài version khác.
  - Nếu vẫn lỗi, xem Unity Console để lấy thông tin chi tiết.

### Sự cố AdMob Adapters

- Không mở được `AdMob Adapters`:
  - Kiểm tra Google Mobile Ads đã được cài trong project chưa.
- AdMob adapter install thất bại:
  - Kiểm tra kết nối mạng.
  - Kiểm tra version được chọn có tồn tại.
  - Xem Console để đọc thông báo lỗi chi tiết.
- Không thấy dialog XML:
  - Đảm bảo bạn đang đi theo nhánh `Use compatible version`.
  - Kiểm tra support Android/iOS có được load thành công không.

### Sự cố AppLovin Adapters

- Không mở được `AppLovin Adapters`:
  - Kiểm tra AppLovin MAX SDK/plugin đã được cài trong project chưa.
- AppLovin adapter install thất bại:
  - Kiểm tra kết nối mạng.
  - Kiểm tra version được chọn có tồn tại.
  - Xem Console để đọc thông báo lỗi chi tiết.
- AppLovin adapter cài xong nhưng không hiển thị đúng trạng thái:
  - Chạy lại dependency resolve trong project.
  - Mở lại Unity Editor window để đồng bộ trạng thái hiển thị.
- AppLovin adapter bị lệch support version Android/iOS:
  - Kiểm tra lại version plugin hiện tại.
  - Ưu tiên version adapter tương thích hơn.
  - Nếu có cảnh báo XML compatibility, xác nhận cập nhật theo hướng downgrade để tránh xung đột.

## Ghi chú

- Dữ liệu version/support có sử dụng cache để tối ưu tốc độ UI.
- Các lựa chọn version được lưu để giữ trạng thái làm việc khi mở lại cửa sổ.

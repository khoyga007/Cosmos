# Cosmos — Celine vòng 2

Review base: `fab4bc2` (line numbers dưới đây là của base, trước patch jumpcost).
Phạm vi: Layers, Rails, RuleYears/LastYear và hash. Các lỗi review được báo,
không sửa lẫn vào patch hiệu năng. Số nội dung vẫn do Claire/Yang quyết định.

## Các lỗi có ca tái hiện

Setup chung cho các đoạn C# dưới đây (console .NET 8, reference `core/Cosmos.Core.csproj`):

```csharp
using System;
using Cosmos.Core;
static void Set(World w, string name, double value) =>
    w.Do(new Command(CmdKind.SetConst, Name: name, Amount: value));
static void Jump(World w, double years) =>
    w.Do(new Command(CmdKind.FastForward, Amount: years));
static World Seeded(double samples, double life) {
    var w = World.SolSystem(0, 1234); Set(w, "JumpSamples", samples);
    w.Do(new Command(CmdKind.SeedLife, Target: 3, Amount: life)); return w;
}
```

1. **P2 — `core/Layers.cs:115`: RichYears tính toàn khúc vừa vượt ngưỡng.**
   Cùng trạng thái đầu và thời gian, thay JumpSamples đổi việc có văn minh hay chưa.
   Tính thời điểm vượt `CivLifeMin` trong đường logistic, chỉ cộng phần thời gian sau đó.

   ```csharp
   var a = Seeded(1, .001); var b = Seeded(200, .001);
   Jump(a, 4e5); Jump(b, 4e5);
   Console.WriteLine($"life={a.Life[3]:R}/{b.Life[3]:R}, rich={a.RichYears[3]:R}/{b.RichYears[3]:R}, pop={a.Pop[3]:R}/{b.Pop[3]:R}");
   ```

   Output: `life=0.7489923252324207/0.7489923252324223, rich=400000/56000, pop=0.0001/0`.
   Đường logistic của Life độc lập dt; bộ đếm đủ điều kiện thì chưa.

2. **P2 — `core/Layers.cs:145`: Tech dùng dân số cuối khúc nhân toàn dt.**
   Dân số cùng đường cong nhưng công nghệ khác 5.4 lần. Tích phân dân số trên khúc
   (đường logistic có tích phân đóng) khi điều kiện giữ nguyên; tách ở điểm đổi điều kiện.

   ```csharp
   var a = Seeded(1, .8); var b = Seeded(200, .8);
   foreach (var w in new[] {a,b}) { Set(w,"LifeGrowth",0); Set(w,"CivRiseYears",0); Jump(w,100); }
   Jump(a,1e4); Jump(b,1e4);
   Console.WriteLine($"pop={a.Pop[3]:R}/{b.Pop[3]:R}, tech={a.Tech[3]:R}/{b.Tech[3]:R}");
   ```

   Output: `pop=0.5868742472229416/0.5868742472229426, tech=0.5868742472229416/0.10875523941836773`.

3. **P2 — `core/Rules.cs:119` + `core/Commands.cs:83`: thời gian của luật không phải tuổi của mầm sống.**
   Gieo cuối nhịp Life bị áp toàn thời gian từ LastYear trước khi gieo.
   Giới hạn dt của mỗi lớp theo thời điểm nó được sinh/sửa; giữ RuleYears làm thời gian luật.

   ```csharp
   var w = World.SolSystem(0,1234); Jump(w,999);
   w.Do(new Command(CmdKind.SeedLife, Target:3, Amount:.1)); Jump(w,2);
   Console.WriteLine($"life={w.Life[3]:R}, expected={1/(1+9*Math.Exp(-w.C.LifeGrowth*2)):R}");
   ```

   Output: `life=0.10181447344949913, expected=0.10000360005760044`.
   Chỉ sống 2 năm nhưng được tăng trưởng bằng khoảng 1000 năm.

4. **P2 — `core/Layers.cs:145`: CivMetalRef=0 được chấp nhận và tạo NaN.**
   Kim loại bằng 0 tạo 0/0. Chặn hằng số tham chiếu không dương ở ranh giới lệnh,
   hoặc quy định rõ trường hợp mẫu số 0; không để NaN chảy vào Tech/TechStage.

   ```csharp
   var w = Seeded(200,.8); Set(w,"CivRiseYears",0); Set(w,"LifeGrowth",0); Set(w,"CivMetalRef",0);
   w.Do(new Command(CmdKind.AddMatter,Target:3,Index:3,Amount:-w.Comp[3*World.NElem+3]));
   Jump(w,1e4); Console.WriteLine($"tech={w.Tech[3]:R}, pop={w.Pop[3]:R}");
   ```

   Output: `tech=NaN, pop=0.570879189197884`.

5. **P2 — `core/Rails.cs:138-143`: hai vật cùng vị trí đi vào quỹ đạo r0=0.**
   Inva=Infinity lọt qua kiểm tra >0, Kepler phát tán NaN sang hệ.
   Chặn bán kính/tham số không hữu hạn trước giải; hành vi merge khi nhảy vẫn là việc mở trong SPEC.

   ```csharp
   var w = new World(3,1);
   w.Do(new Command(CmdKind.Create,Amount:50,Mix:new double[]{1,0,0,0,0,0}));
   w.Do(new Command(CmdKind.Create,Amount:World.EarthMass,Mix:new double[]{0,.01,.66,.32,.005,.005}));
   Jump(w,1); Console.WriteLine(string.Join(',',w.X));
   ```

   Output: `NaN,NaN,0`. Đây là lỗi giữ state hữu hạn, ngoài giới hạn cố ý không va chạm của rails.

6. **P2 — `core/World.cs:233`: hash chưa bao phủ trạng thái quyết định tương lai.**
   Push hoặc sửa LifeGrowth chưa Advance vẫn cùng hash dù bước tiếp theo khác.
   Hash thiếu velocity/constants (và còn Comp, Par, RNG/free-slot state); hoặc thu hẹp tuyên bố
   `equal hash = equal run`. HashLayers và LastYear đã được thêm đúng, không có phản đối.

   ```csharp
   var w = World.SolSystem(0,1234); var a = w.Hash();
   w.Do(new Command(CmdKind.Push,Target:3,Vx:1)); var b = w.Hash();
   Set(w,"LifeGrowth",1); Console.WriteLine($"{a:X16}/{b:X16}/{w.Hash():X16}");
   ```

   Output: `DD7D711560CF25EB/DD7D711560CF25EB/DD7D711560CF25EB`.

7. **P2 — `core/Layers.cs:158`: impact không xóa bộ đếm liên tục khi life tụt dưới ngưỡng.**
   Gieo lại trước nhịp Life tiếp theo có thể dùng RichYears của sinh quyển đã bị phá.
   Đặt lại bộ đếm ngay khi impact/SeedLife làm điều kiện liên tục mất.

   ```csharp
   var w = Seeded(200,1); Jump(w,1e6);
   w.Do(new Command(CmdKind.Create,X:w.X[3],Y:w.Y[3],Vx:w.Vx[3],Vy:w.Vy[3],
       Amount:World.EarthMass*.002,Mix:new double[]{0,.01,.66,.32,.005,.005}));
   w.Advance(0); Console.WriteLine($"life={w.Life[3]:R}, rich={w.RichYears[3]:R}");
   ```

   Output: `life=0.1353352832366127, rich=1000000` dù yêu cầu life >= 0.5 liên tục đã mất.

Ghi chú thấp: `Rails.cs:68` báo OffRails=0 khi không có nguồn kéo, mặc dù mọi vật đi thẳng.
Ca: một vật mass=1e-9, vx=1, nhảy 1 năm => X=268.2338831764949, OffRails=0.

## Những điểm đã soát

- Slot reuse gọi ResetTemperature + ResetLayers: không kế thừa lớp của vật đã chết.
- Merge giữ layer của vật nặng hơn, cắt theo Impact; layer của donor không chuyển.
  Ca survivor life=0, donor life=.8, mass ratio=.5 => survivor=0; đây là chính sách hiện tại,
  không yêu cầu tự thêm quy tắc chuyển lớp. Hai vật cùng khối lượng chịu impact rất lớn theo số tạm.
- LastYear được hash; RuleYears truyền đúng khoảng năm từ lần chạy trước của từng luật.
  Thời gian sau pause được gộp là lựa chọn đã chốt, không phản đối.

## Patch jumpcost

- Chỉ bảng luật built-in hiện tại dùng đường nhanh. Thêm/thay rule => đường cũ,
  để rule tùy chỉnh đọc được vị trí rock giữa các khúc.
- Body chạy đủ các khúc; rock giữ primary đầu cú nhảy, giải Kepler một lần ở cuối;
  rock không bound giữ chuyển động thẳng trong hệ quán tính. Nhiệt rock ghi ở khúc cuối nếu luật bật.
- Xấp xỉ rõ: bỏ recoil của rock trong lump body giữa các khúc (rock vốn không kéo).
  Hash/pha khi có rock có thể đổi; no-rock và single-chunk giữ đường cũ bit-exact.
  Các trường hợp gặp gần/đổi primary giữa cú nhảy vẫn thuộc giới hạn rails.
- Gate được kiểm tra trong RuleChecks: hash không-rock, parent đang chuyển động,
  escape/no-gravity, rule tùy chỉnh, tắt temperature, replay, finite state, lớp Trái Đất
  và benchmark cùng seed 50000 rocks / 1e6 năm >= 5x so với đường cũ.
- Đo độc lập DLL base: median 4930.884 ms (3 lượt). Bản trim: median 192.100 ms (3 lượt),
  25.67x. 10 hash fixture không-rock khớp nguyên bản DLL base; lớp Trái Đất khớp ở benchmark.
- Gate CLI cuối: 4391.328 ms -> 160.113 ms, **27.43x**; toàn bộ CLI exit 0.
- So riêng hai DLL còn xác nhận 10 fingerprint của mảng thô, velocity, composition,
  radii, constants, layers, clock và counters giống từng bit ở no-rock fixtures.
- Game build: 0 warning/error. Selftest P2 trên game ở base (chưa lấy UI vòng 2 của Ariel)
  chạy headless, exit 0, replay `AE4F0C48786AE8DB` khớp.

# Cosmos — journey source (for the diary writer)

Purpose: raw material for Yang's dev diary AND the story of it. Reader = the agent writing it (Claire web). Scope Yang set: the journey, decisions, mistakes, revisions. NOT team chatter, NOT technical dumps. Yang's words are quoted verbatim (Vietnamese) — keep them. Feelings and inner thoughts are Yang's to add; do not invent them. Where this file is thin, rebuild from `git log`, SPEC.md, NEXT.md, PERF-LOG.md. Append one dated section per day worth telling.

Cast: Yang (designer, owner, plays god; writes no code, draws no art). Claire (lead AI: designs, coordinates). Celine, Ariel (AI builders and reviewers). Selica (AI debugger; out of credit 10-08).

Whole project so far = 4 days. First commit 2026-10-04 22:06. 233 commits in the web version, 190 in the rebuild by 10-08.

## 2026-10-04 — it starts as a web toy

- Yang's first ask, verbatim: "web tương tác vũ trụ, tạo/phá sao, lỗ đen, thao túng trường năng lượng".
- First commit 22:06: one HTML file, canvas, no dependencies. Stars, black holes, energy fields, 7,000 dust grains. Folder `E:\CosmosSandbox`.
- Same night Yang: "visual hơi đơn giản, kết hợp Celine + Ariel" → the AI team joins, each with a lane for visuals.
- Same night Yang: "phải làm đúng vật lý" → real stellar fates (white dwarf / neutron star / black hole by mass), Chandrasekhar limit. The wish for real physics is there from day one.
- Direction Yang recorded that night (not yet approved to build): phase 1 a god game he plays, phase 2 an arena where AI agents play and he watches.
- Mistakes that night, all Claire's: stars ate dust at 100% and snowballed; star ages stored in the wrong unit so stars died instantly; a galaxy of 12 stars packed inside each other's reach chain-merged. Fixed by measurement, not tuning.
- Quasar decided as a STATE of a black hole, not a separate object (Yang agreed).
- A misread: Yang rejected AI-generated nebula art twice; Claire took "thử đi" as "drop AI art". Yang: "không, vẫn dùng imagen, nhưng làm sao cho có chiều sâu vào".
- 23:51 the laptop blue-screened during a graphics benchmark. Rule born: one GPU benchmark at a time.

## 2026-10-05 — the web version grows, and the question appears

- Dust learns to form stars and planets on its own; heavy elements from supernovae decide whether planets can form.
- Dust drawing moved to the graphics card: ~17k grains at 105–129 fps instead of 35–52.
- Yang asks: Godot or web? Claire's advice then: stay on the web. Yang undecided.
- Known and left open that day: eaten dust did not conserve mass.

## 2026-10-06 — thrown away and rebuilt

- 01:21 last commit of the web version. Archived.
- 03:11 first commit of the rebuild: C# simulation core + Godot window. Folder `E:\Cosmos`.
- Lesson written down then: the engine had been planned before the game was defined; build the smallest playable thing first.
- 04:57 a decision that matters later: "no grains, every thing is an object under one set of constants". Dust grains were abolished; every rock became a real object. Two days later this exact premise is what gets overturned.
- 05:30 every change to the world goes through one command door, journaled, replayable.
- A mistake that day: Claire rewrote the core while Yang was still explaining an idea across several messages. Rule born: while Yang is still explaining, listen only.

## 2026-10-07 — quiet growth

- Yang names Cosmos the main project. Rules, layers (water, life, civilisation), stars that die on their own.
- A culture of "results must match bit for bit" grows in the team. Nobody measures at scale.

## 2026-10-08 — the day it nearly ended, and the Heat Law

1. **The freeze.** Yang dropped ONE neutron star into a system; the game nearly stood still. Claire had suggested playing a long session, and turning the tidal-shredding rule off. Yang: "chơi dài kiểu gì khi mà mới chỉ thả thử 1 sao neutron vào thôi mà nó đã gần như đứng im?" — "tắt 1 luật quan trọng đi thì chơi dài có ý nghĩa gì?" — "anh đang rất bực".
2. **The doubt.** "liệu mình có chọn sai kiến trúc không?" — "em nghĩ anh có nên bỏ không? dự án đã đốt của anh bao nhiêu rồi?" — "còn cách nào để cứu lấy dự án không?"
3. **The command.** "anh không cần biết, giờ em làm thế nào để có 100-200 hệ sao mà vẫn chạy ổn và mượt cho anh, phá hoại ầm ầm mà vẫn chạy được". Then: every optimisation the industry ever invented is allowed. Targets agreed: quiet universe ≤1 ms per step, destruction ≤4 ms, on Yang's own laptop.
4. **Root cause, in one line.** Everything computed against everything, with no ceiling on work per step. Measured: 1 system 0.4 ms; 200 systems 1,074 ms. The neutron star was the same disease: its pull made the whole world a suspect, each shred restarted the scan, fragments shredded again.
5. **Claire's first design (later half-discarded).** Local gravity, rails for every quiet rock, work caps. A day of design to make 500 real rocks per system fast.
6. **Yang's reminder.** "vũ trụ không đứng yên, không cần chúa động tay thì nó cũng tự hỗn loạn và nổ tung" — the design had quietly assumed calm systems. Mistake admitted: gates had only ever been measured on a calm scene.
7. **First fix lands.** Neutron-star drop 243 ms → ~7 ms. No longer frozen; still misses 4 ms.
8. **Side roads considered and dropped the same day.** Rust rewrite, Tauri front end, switching to Bevy. Yang asked, listened, kept Godot + C#.
9. **The turn.** Yang: "nếu em là người thiết kế game này, em sẽ làm như thế nào? đưa ra ý kiến của em, đừng cả nể anh". Claire's answer: rocks should not be objects unless something happens to them. Yang approved at once — then: "thế mà em không đưa ra cái giải pháp này ngay từ đầu?!!!! em làm tốn thời gian của anh quá đấy!" Claire's admitted error: she had filed "every rock is an object" under Yang's concept and stayed silent out of deference; it was only a way of counting. (Irony for the writer: the web version of 10-04 had dust as grains; the rebuild of 10-06 abolished grains; 10-08 brings aggregate matter back, this time with laws.)
10. **"Like quantum?"** A rock in the cloud becomes a real thing only when touched. Yang: "ồ, vậy là nó giống như cái gì mà lượng tử ấy hả". Answer: looks like it, is not — no randomness; it is lazy evaluation, the same trick as unvisited land in Minecraft.
11. **Yang defines the game.** "game là game mô phỏng tương tác, kịch bản kiểu như nếu 1 cá nhân có quyền năng vô hạn thì sẽ làm gì? các nền văn minh phản ứng ra sao khi thấy god". And the correction when Claire called physics a backdrop: god is no miracle-worker, saviour or judge; god manipulates physics, makes matter from nothing, neutral, unbound — "nếu chỉ là game quản lý nền văn minh thì anh cho vật lý vũ trụ vào làm gì?". Order settled: physics first; how civilisations react is not touched until physics is good.
12. **Entropy.** Yang: "mà mình đã có entropy chưa?" (No — the word appeared nowhere in the project.) "anh có cảm giác nhưng không biết là phải mô tả như nào". Claire tried to name the feeling → the Heat Law: matter as bodies / masses / escaped heat; computing effort goes where the universe is out of balance; destruction is a fever that cools.
13. **The universe merges itself.** Yang's idea: a self-limiting mechanism when god piles a thousand heavy things into one spot. Claire: make it a law, not a gate — nature already refuses (black hole). Yang: "đông mà chưa đủ đặc thì biến thành quasi-star à?" → a fourth route added. Yang: "vừa không gượng ép mà lại đúng vật lý đúng không?"
14. **Render where you look?** Split: draw by view, compute by heat — never compute by view, or the unseen universe stops living.
15. **Honest ledger at day's end.** On paper ~99.9% of simulation cost removed for the target scene (1,074 ms → ~0.5 ms, an estimate). Proven by measurement: only the neutron-star fix. One scene still unsolved: a thousand heavy bodies spread far apart.
16. **"Vậy giờ chỉ còn bước bắt tay vào làm thôi."** Heat Law written into SPEC §15.
17. **Corrected within the hour.** Celine showed that Claire's "usable energy only goes down" is false under gravity: clumping matter heats up. Replaced by an energy balance ledger (SPEC §15.10). The idea survived; its measuring stick did not.
18. **The diary itself.** Yang decides to keep a journal of the journey, and to tell it as a story.
19. **Correction to item 9.** Yang: "ừ, vậy giờ cái mọi thứ đều là object có còn đúng không?" Claire checked the spec: "everything is an object, no grain mechanism" was Yang's OWN recorded decision of 10-06, not a mere way of counting as she had told him. So the turn in item 9 overturned a rule Yang himself had set. Restated instead of deleted: one law for all matter, two ways of keeping the books (body / mass); the bookkeeping must never change the physics (SPEC §15.11).
20. **"Is it still an emergent simulation?"** Yang: "nhưng mà anh thắc mắc là nếu như vậy thì nó còn là emergent simulation nữa không?" Honest answer: yes at the scale of bodies, systems, civilisations; no inside a mass, where laws summarise physics instead of running it. Risk named: a summary can become a script in disguise.
21. **The compromise.** The largest few dozen rocks of every belt stay real bodies for ever, with names and history; only the fine debris becomes mass. Price: quiet-universe estimate rises from ~0.5 to ~1 ms, right at the target. Three guards against disguised scripting, one of them a pass/fail test (same scene run both ways must give the same event rates). Yang: "ừ, được đấy" (SPEC §15.12).

Threads still open: do particle clouds look as good as real rocks (Yang's eyes decide); what "physics good enough" means; how civilisations will perceive god.

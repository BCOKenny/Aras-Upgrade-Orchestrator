import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";
import { pathToFileURL } from "node:url";

const { SKILL_DIR, TMP_DIR, FINAL_PPTX } = process.env;
const { resolvePresentationFont } = await import(pathToFileURL(path.join(SKILL_DIR, "container_tools/artifact_tool_utils.mjs")).href);
await fs.mkdir(TMP_DIR, { recursive: true });
await fs.mkdir(path.dirname(FINAL_PPTX), { recursive: true });

const family = "Microsoft JhengHei";
const p = Presentation.create({ slideSize: { width: 1280, height: 720 } });
const C = { navy: "#123047", blue: "#2B6F9E", teal: "#2D8C87", amber: "#E6A23C", ink: "#20313D", muted: "#60727D", pale: "#F3F7F9", white: "#FFFFFF", line: "#D7E2E8" };

function box(slide, x, y, w, h, fill, radius=false) {
  return slide.shapes.add({ geometry: radius ? "roundRect" : "rect", position: { left:x, top:y, width:w, height:h }, fill, line: { fill: fill, width: 0 } });
}
function text(slide, value, x, y, w, h, size=22, color=C.ink, bold=false, align="left") {
  const s = slide.shapes.add({ geometry: "textbox", position: { left:x, top:y, width:w, height:h }, fill: "none", line: { fill: "none", width: 0 } });
  s.text = value;
  s.text.style = { typeface: family, fontSize: size, color, bold, autoFit: "shrink", align, verticalAlign: "mid" };
  return s;
}
function title(slide, t, sub="") { text(slide, t, 72, 42, 1136, 48, 34, C.navy, true); if(sub) text(slide, sub, 74, 94, 1100, 30, 17, C.muted); }
function footer(slide, n) { text(slide, `階段性開發成果  |  ${n}/6`, 72, 682, 1136, 18, 12, C.muted); }
function bullet(slide, items, x, y, w, size=21, gap=43) { items.forEach((v,i)=>{ text(slide, "•", x, y+i*gap, 20, 28, size, C.teal, true); text(slide, v, x+28, y+i*gap, w-28, 34, size, C.ink, false); }); }

// 1 Cover
{ const s=p.slides.add(); s.background.fill=C.navy; box(s, 72, 98, 10, 250, C.teal); text(s, "階段性開發成果", 112, 132, 980, 70, 48, C.white, true); text(s, "客戶環境升級與 Core Tree 整合交付流程", 114, 218, 980, 48, 28, "#D9EAF2", false); text(s, "芯洲  |  11.0 SP9 → R38", 114, 332, 700, 36, 22, "#A9CBD8", false); text(s, "本階段成果：完成 Core Tree 比較程序與 A／B／C 交付", 114, 505, 900, 34, 19, C.white, false); text(s, "BCO\\kenny  ·  2026-09", 114, 610, 600, 24, 15, "#A9CBD8"); }

// 2 requirements
{ const s=p.slides.add(); s.background.fill=C.white; title(s, "需求訪談形成的整體規格", "規格名稱：客戶環境升級與 Core Tree 整合交付流程"); box(s,72,150,520,430,C.pale,true); text(s,"流程目標",100,178,430,34,24,C.blue,true); bullet(s,["定義來源版本升級至 R38 的標準作業流程","確保跳點依版本順序執行","同時完成 R38 DB 與 Core Tree 最終交付"],100,230,445,20,74); box(s,640,150,568,430,"#EEF7F6",true); text(s,"適用範圍",668,178,470,34,24,C.teal,true); bullet(s,["客戶環境重建與客戶 Package 產生","各跳點 Package 準備、核准與執行","客戶 Core Tree 與 R38 OOTB 的比較、合併及交付"],668,230,490,20,74); footer(s,2); s.speakerNotes.textFrame.setText("來源：需求訪談內容與專案升級規格。"); }

// 3 workflow
{ const s=p.slides.add(); s.background.fill=C.white; title(s, "端到端升級與整合流程", "Package／DB 與 Core Tree 分流準備，最終在交付階段整合"); const steps=["環境重建","客戶 Package\n基準","跳點 Package\n準備與核准","依序執行\nDB 跳點","R38 DB\n備份","Core Tree\n比較與分類","人工確認與\n最終交付"]; const colors=[C.blue,C.blue,C.amber,C.amber,C.amber,C.teal,C.teal]; steps.forEach((v,i)=>{const x=72+i*166;box(s,x,245,136,116,colors[i],true);text(s,`${i+1}`,x+12,258,30,28,18,C.white,true);text(s,v,x+12,294,112,54,19,C.white,true,"center");if(i<steps.length-1){box(s,x+136,298,30,6,C.line);}}); box(s,72,430,1136,116,"#FFF8EA",true); text(s,"跳點順序關卡",100,450,230,30,22,C.amber,true); text(s,"11SP5 → 11SP15 → 12SP18 → R38；每一跳必須先完成 Package 產生、驗證、核准與執行結果確認。",330,450,820,52,20,C.ink,false); footer(s,3); s.speakerNotes.textFrame.setText("來源：需求訪談規格；本頁為流程概念圖，Package／DB 與 Core Tree 仍是獨立工作流。"); }

// 4 gates
{ const s=p.slides.add(); s.background.fill=C.white; title(s, "關卡與證據設計", "每個階段都有可追溯的輸入、判定與停止條件"); const rows=[['輸入證據','三份 Core Tree、版本證據、完整性清單','缺失或不一致即停止'],['Preflight','版本、Client／Server 結構、路徑隔離、Server 規則','僅 Ready 可進入比較'],['比較與 review','A／B／C 分類、人工確認清單、不可變 attempt','未解決 review 不得完成'],['最終交付','Completion receipt、delivery manifest、檔案 checksum','輸入與原始 attempt 保持 immutable']]; rows.forEach((r,i)=>{const y=155+i*108;box(s,72,y,210,78,i%2?"#EEF7F6":C.pale);box(s,282,y,570,78,C.white);box(s,852,y,356,78,i%2?"#FFF8EA":"#FBEFEF");text(s,r[0],92,y+20,170,34,20,C.navy,true);text(s,r[1],304,y+17,530,42,18,C.ink);text(s,r[2],874,y+17,320,42,18,i%2?"#8A621B":"#9A3940",true);}); text(s,"核心原則：輸入唯讀、輸出使用新目錄、歷程只追加，任何不確定性都保留為人工處置。",74,604,1100,34,19,C.muted,true); footer(s,4); }

// 5 current result
{ const s=p.slides.add(); s.background.fill=C.white; title(s, "目前完成成果：Core Tree 比較程序", "芯洲案件已完成從輸入證據到正式 A／B／C delivery 的流程"); box(s,72,150,330,370,C.navy,true); text(s,"已完成",104,182,250,36,26,C.white,true); text(s,"Preflight",104,250,220,30,21,"#BDE4E1",true); text(s,"Comparison",104,300,220,30,21,"#BDE4E1",true); text(s,"Review approval",104,350,240,30,21,"#BDE4E1",true); text(s,"Finalization",104,400,220,30,21,"#BDE4E1",true); text(s,"A/B/C delivery",104,450,250,30,21,"#BDE4E1",true); box(s,450,150,758,370,C.pale,true); text(s,"正式產出",482,182,260,36,26,C.blue,true); const metrics=[['A 類','377'],['B 類','15'],['C 類','18'],['交付檔案','462']]; metrics.forEach((m,i)=>{const x=482+(i%2)*330,y=250+Math.floor(i/2)*105; text(s,m[1],x,y,120,48,34,C.teal,true);text(s,m[0],x+135,y+7,160,34,20,C.ink,true);}); text(s,"人工 review：1 筆，已由 BCO\\kenny 處置為 A。",482,470,650,32,18,C.ink); footer(s,5); s.speakerNotes.textFrame.setText("來源：芯洲案件正式 CLI 執行結果。比較 attempt 保留為 Incomplete，completion receipt 與 delivery manifest 分別保存流程完成與交付完成證據。"); }

// 6 outputs next
{ const s=p.slides.add(); s.background.fill=C.white; title(s, "產出物與後續工作邊界", "本階段完成 Core Tree 交付，Package／DB 升級仍需獨立流程"); box(s,72,150,530,390,"#EEF7F6",true); text(s,"本階段產出",104,182,380,34,25,C.teal,true); bullet(s,["正式案件清單與 Core Tree 設定","三組輸入 Evidence Set","比較 attempt、review approval 與 completion receipt","A／B／C delivery 與 delivery manifest"],104,240,450,20,67); box(s,650,150,558,390,"#FFF8EA",true); text(s,"後續仍需處理",682,182,430,34,25,C.amber,true); bullet(s,["依正式 SOP 建立並驗證 Package／DB 跳點","保存每跳 DB 備份、登入驗證與執行紀錄","由操作人員執行最終環境整合與驗收"],682,240,470,20,80); text(s,"結論：Core Tree 比較程序已具備可追溯、可核驗的正式產出。",74,602,1120,34,23,C.navy,true,"center"); footer(s,6); }

const out = path.join(TMP_DIR, "stage-report-draft.pptx");
await (await PresentationFile.exportPptx(p)).save(out);
console.log(out);

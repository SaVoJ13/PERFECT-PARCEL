// Незалежна модель Агента 5 (QA) для E0-07. Не копія коду: порт семантики C# (SaveJson, SaveSerializer,
// SaveService, InMemorySaveStorage) з читання вихідних файлів коміту 16a536a, щоб перевірити поза Unity:
//  A) зафіксовані тексти v1/v2 і результат міграції;
//  B) межові випадки розбирача;
//  C) перебір збою на кожній операції Save з усіх початкових станів файлів;
//  D) сценарій «тимчасова помилка читання під час Load».
// Запуск: node independent_save_model_v2.js (v2 — повторна перевірка після b1f702e)
'use strict';
const LMIN = -(2n ** 63n), LMAX = 2n ** 63n - 1n;
class FormatErr extends Error {}
class TooNew extends FormatErr { constructor(v) { super('too new ' + v); this.v = v; } }
class IOErr extends Error {}

function parse(text) {
  if (text == null) throw new FormatErr('null');
  let pos = 0;
  const peek = () => { if (pos >= text.length) throw new FormatErr('eof'); return text[pos]; };
  const expect = c => { if (peek() !== c) throw new FormatErr('expected ' + c); pos++; };
  const ws = () => { while (pos < text.length && ' \t\n\r'.includes(text[pos])) pos++; };
  function str() {
    expect('"'); let out = '';
    for (;;) {
      const c = peek(); pos++;
      if (c === '"') return out;
      if (c < ' ') throw new FormatErr('ctrl');
      if (c !== '\\') { out += c; continue; }
      const e = peek(); pos++;
      if ('"\\/'.includes(e)) out += e;
      else if (e === 'b') out += '\b'; else if (e === 'f') out += '\f'; else if (e === 'n') out += '\n';
      else if (e === 'r') out += '\r'; else if (e === 't') out += '\t';
      else if (e === 'u') {
        const h = text.substr(pos, 4);
        if (h.length < 4 || !/^[0-9a-fA-F]{4}$/.test(h)) throw new FormatErr('bad \\u');
        out += String.fromCharCode(parseInt(h, 16)); pos += 4;
      } else throw new FormatErr('bad escape');
    }
  }
  function num() {
    const s = pos; let integer = true;
    while (pos < text.length) {
      const c = text[pos];
      if (c === '.' || c === 'e' || c === 'E') integer = false;
      else if (!(/[0-9]/.test(c) || c === '-' || c === '+')) break;
      pos++;
    }
    const t = text.slice(s, pos);
    // long.TryParse(AllowLeadingSign, Invariant): необов'язковий знак + цифри, у межах long
    if (integer && /^[+-]?[0-9]+$/.test(t)) { const b = BigInt(t); if (b >= LMIN && b <= LMAX) return { long: b }; }
    // double.TryParse(Float): знак, цифри, крапка, експонента
    if (/^[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$/.test(t)) return { dbl: Number(t) };
    throw new FormatErr('bad number "' + t + '"');
  }
  function lit(w) { if (text.substr(pos, w.length) !== w) throw new FormatErr('literal'); pos += w.length; }
  function val(d) {
    if (d > 32) throw new FormatErr('depth');
    ws(); const c = peek();
    if (c === '{') {
      const o = new Map(); expect('{'); ws(); if (peek() === '}') { pos++; return o; }
      for (;;) { ws(); const k = str(); ws(); expect(':'); o.set(k, val(d + 1)); ws(); if (peek() === ',') { pos++; continue; } expect('}'); return o; }
    }
    if (c === '[') {
      const a = []; expect('['); ws(); if (peek() === ']') { pos++; return a; }
      for (;;) { a.push(val(d + 1)); ws(); if (peek() === ',') { pos++; continue; } expect(']'); return a; }
    }
    if (c === '"') return str();
    if (c === 't') { lit('true'); return true; }
    if (c === 'f') { lit('false'); return false; }
    if (c === 'n') { lit('null'); return null; }
    return num();
  }
  const v = val(0); ws(); if (pos !== text.length) throw new FormatErr('tail'); return v;
}
function appendStr(s) {
  let o = '"';
  for (let i = 0; i < s.length; i++) { // по UTF-16 char, як foreach (char c in value) у C#
    const ch = s[i];
    if (ch === '"' || ch === '\\') o += '\\' + ch;
    else if (ch < ' ') o += '\\u' + ch.charCodeAt(0).toString(16).padStart(4, '0');
    else o += ch;
  }
  return o + '"';
}
function ordinal(a, b) { // StringComparer.Ordinal = порівняння UTF-16 code units
  for (let i = 0; i < Math.min(a.length, b.length); i++) { const x = a.charCodeAt(i) - b.charCodeAt(i); if (x) return x; }
  return a.length - b.length;
}
function serialize(d) {
  const keys = [...d.stars.keys()].sort(ordinal);
  return '{"schemaVersion":2,"coins":' + d.coins + ',"gems":' + d.gems + ',"levelStars":{' +
    keys.map(k => appendStr(k) + ':' + d.stars.get(k)).join(',') +
    '},"lastSavedUnixSec":' + d.saved + ',"lastKnownUnixSec":' + d.known + '}';
}
function getLong(o, k) { const v = o.get(k); if (!v || typeof v !== 'object' || !('long' in v)) throw new FormatErr('field ' + k); return v.long; }
function nonNeg(o, k) { const v = getLong(o, k); if (v < 0n) throw new FormatErr('neg ' + k); return v; }
function deserialize(text) {
  const root = parse(text);
  if (!(root instanceof Map)) throw new FormatErr('root');
  let ver = getLong(root, 'schemaVersion');
  if (ver < 1n) throw new FormatErr('ver');
  if (ver > 2n) throw new TooNew(ver);
  const migrated = ver < 2n;
  for (; ver < 2n; ver++) {
    if (ver === 1n) root.set('lastKnownUnixSec', { long: getLong(root, 'lastSavedUnixSec') });
    else throw new FormatErr('no migration');
    root.set('schemaVersion', { long: ver + 1n });
  }
  const d = { coins: nonNeg(root, 'coins'), gems: nonNeg(root, 'gems'), saved: nonNeg(root, 'lastSavedUnixSec'), known: nonNeg(root, 'lastKnownUnixSec'), stars: new Map() };
  const st = root.get('levelStars'); if (!(st instanceof Map)) throw new FormatErr('stars');
  for (const [k] of st) { const v = nonNeg(st, k); if (v > 2147483647n) throw new FormatErr('stars big'); d.stars.set(k, v); }
  return { d, migrated };
}

// --- сховище в пам'яті з тією ж семантикою, що InMemorySaveStorage, + ін'єкція збою (як FaultyStorage) ---
class Store {
  constructor(files) { this.f = new Map(Object.entries(files || {})); this.ops = 0; this.failFrom = Infinity; this.failRead = new Set(); this.torn = false; }
  step() { this.ops++; if (this.ops >= this.failFrom) throw new IOErr('crash'); }
  exists(n) { this.step(); if (this.failExists && this.failExists.has(n)) throw new IOErr("exists failed"); return this.f.has(n); }
  read(n) { this.step(); if (this.failRead.has(n)) throw new IOErr('transient read error'); if (!this.f.has(n)) throw new IOErr('nf'); return this.f.get(n); }
  write(n, t) {
    this.ops++;
    if (this.ops >= this.failFrom) { if (this.torn && this.ops === this.failFrom) this.f.set(n, t.slice(0, t.length >> 1)); throw new IOErr('crash'); }
    this.f.set(n, t);
  }
  move(a, b) { this.step(); if (!this.f.has(a)) throw new IOErr('nf'); if (this.f.has(b)) throw new IOErr('exists'); this.f.set(b, this.f.get(a)); this.f.delete(a); }
  del(n) { this.step(); this.f.delete(n); }
}
const P = 'save.json', B = 'save.json.bak', T = 'save.json.tmp', C = '.corrupt';
// v2: семантика коміту b1f702e (стан Unavailable, .corrupt/.corrupt.2/.corrupt.3, RejectedInvalidData, CanSave-поле)
function inspect(s, n) {
  let text;
  try { if (!s.exists(n)) return { k: 'Missing' }; text = s.read(n); } catch (e) { return { k: 'Unavailable' }; }
  try { return { k: 'Valid', ...deserialize(text) }; } catch (e) { if (e instanceof TooNew) return { k: 'Newer' }; return { k: 'Corrupt' }; }
}
const MAXC = 3;
const cname = (n, i) => i <= 1 ? n + C : n + C + '.' + i;
function quarantine(s, n) { let i = 1; while (i < MAXC && s.exists(cname(n, i))) i++; const t = cname(n, i); if (s.exists(t)) s.del(t); s.move(n, t); }
function tryQ(s, n) { try { quarantine(s, n); } catch (e) { if (!(e instanceof IOErr)) throw e; } }
function load(s) {
  const p = inspect(s, P);
  if (p.k === 'Unavailable') return { src: 'Unavailable', reset: false, corrupt: 0, canSave: false };
  if (p.k === 'Newer') return { src: 'NewerVersion', reset: false, corrupt: 0, canSave: false };
  const b = inspect(s, B);
  if (p.k === 'Valid') return { src: 'Primary', d: p.d, reset: false, corrupt: 0, canSave: b.k !== 'Newer' && b.k !== 'Unavailable' };
  if (b.k === 'Unavailable') return { src: 'Unavailable', reset: false, corrupt: 0, canSave: false };
  let corrupt = 0;
  if (p.k === 'Corrupt') { corrupt++; tryQ(s, P); }
  if (b.k === 'Valid') return { src: 'Backup', d: b.d, reset: false, corrupt, canSave: true };
  if (b.k === 'Newer') return { src: 'NewerVersion', reset: false, corrupt, canSave: false };
  if (b.k === 'Corrupt') { corrupt++; tryQ(s, B); }
  return { src: 'NewProfile', reset: corrupt > 0, corrupt, canSave: true };
}
function canBeLoadedBack(text) {
  try { const r = deserialize(text); if (Buffer.from(text, 'utf8').toString('utf8') !== text) return false; return serialize(r.d) === text; }
  catch (e) { if (e instanceof FormatErr) return false; throw e; }
}
function save(s, d, clock) {
  try {
    const p = inspect(s, P), b = inspect(s, B);
    if (p.k === 'Newer' || b.k === 'Newer') return 'RejectedNewerSave';
    if (p.k === 'Unavailable' || b.k === 'Unavailable') return 'Failed';
    d.known = d.known > clock ? d.known : clock; d.saved = clock > 0n ? clock : 0n;
    const text = serialize(d);
    if (!canBeLoadedBack(text)) return 'RejectedInvalidData';
    s.write(T, text);
    const w = inspect(s, T); if (w.k !== 'Valid' || serialize(w.d) !== text) return 'Failed';
    if (p.k === 'Valid') { if (s.exists(B)) s.del(B); s.move(P, B); } else if (p.k === 'Corrupt') quarantine(s, P);
    s.move(T, P); return 'Saved';
  } catch (e) { if (e instanceof IOErr) return 'Failed'; throw e; }
}
const sample = c => ({ coins: BigInt(c), gems: BigInt(c) / 10n, saved: 0n, known: 0n, stars: new Map([['loc1_01', 3n], ['loc1_02', BigInt(c % 4)]]) });
const out = []; const log = (...a) => out.push(a.join(' '));
let fails = 0; const check = (ok, msg) => { log((ok ? 'OK   ' : 'FAIL ') + msg); if (!ok) fails++; };

// ---------- A. Зафіксовані тексти з SaveTestKit / SaveSerializerTests ----------
const V1 = '{"schemaVersion":1,"coins":120,"gems":5,"levelStars":{"loc1_01":3,"loc1_02":1},"lastSavedUnixSec":1700000000}';
const V1to2 = '{"schemaVersion":2,"coins":120,"gems":5,"levelStars":{"loc1_01":3,"loc1_02":1},"lastSavedUnixSec":1700000000,"lastKnownUnixSec":1700000000}';
const GOLD = '{"schemaVersion":2,"coins":120,"gems":5,"levelStars":{"loc1_01":3,"loc1_02":1},"lastSavedUnixSec":1700000000,"lastKnownUnixSec":1700000500}';
log('== A. Зафіксовані тексти');
{ const r = deserialize(V1); check(r.migrated && serialize(r.d) === V1to2, 'V1Text -> міграція -> V1MigratedToV2Text'); }
{ const r = deserialize(GOLD); check(!r.migrated && serialize(r.d) === GOLD, 'GoldenText (v2) round-trip без міграції'); }
check(serialize({ coins: 0n, gems: 0n, saved: 0n, known: 0n, stars: new Map() }) ===
  '{"schemaVersion":2,"coins":0,"gems":0,"levelStars":{},"lastSavedUnixSec":0,"lastKnownUnixSec":0}', 'новий профіль = зафіксований текст');
{ const d = { coins: 0n, gems: 0n, saved: 0n, known: 0n, stars: new Map([['b', 1n], ['B', 2n], ['a', 3n], ['10', 0n], ['9', 0n]]) };
  check(serialize(d).includes('"levelStars":{"10":0,"9":0,"B":2,"a":3,"b":1}'), 'порядковий (Ordinal) порядок ключів рівнів'); }
try { deserialize('{"schemaVersion":3,"coins":777,"futureField":{"a":[1,2.5,"x"]}}'); check(false, 'NewerVersionText'); }
catch (e) { check(e instanceof TooNew && e.v === 3n, 'NewerVersionText -> TooNew(3)'); }

// ---------- B. Межові випадки розбирача ----------
log('== B. Розбирач');
const ids = ['quote"back\\slash/', 'рівень їжак', 'tab\tnewline\ncontrol\u0001', '🌯', '', ' \u007f', 'é'];
{ const d = { coins: LMAX, gems: 0n, saved: 253402300799n, known: LMAX, stars: new Map(ids.map((k, i) => [k, BigInt(i)])) };
  const t = serialize(d); const r = deserialize(t);
  check(serialize(r.d) === t && r.d.stars.size === ids.length, 'round-trip довільних id (лапки, \\, юнікод, сурогатна пара, керівні, U+2028) і long.MaxValue'); }
const cases = [
  ['"+5" як coins (не JSON)', '{"schemaVersion":2,"coins":+5,"gems":1,"levelStars":{},"lastSavedUnixSec":1,"lastKnownUnixSec":1}'],
  ['провідні нулі "007"', '{"schemaVersion":2,"coins":007,"gems":1,"levelStars":{},"lastSavedUnixSec":1,"lastKnownUnixSec":1}'],
  ['"-0"', '{"schemaVersion":2,"coins":-0,"gems":1,"levelStars":{},"lastSavedUnixSec":1,"lastKnownUnixSec":1}'],
  ['дублікат ключа coins (1, потім 9)', '{"schemaVersion":2,"coins":1,"coins":9,"gems":1,"levelStars":{},"lastSavedUnixSec":1,"lastKnownUnixSec":1}'],
  ['schemaVersion відсутній', '{"coins":1,"gems":1,"levelStars":{},"lastSavedUnixSec":1,"lastKnownUnixSec":1}'],
  ['schemaVersion 1e400', '{"schemaVersion":1e400}'],
  ['schemaVersion 99999999999999999999 (поза long)', '{"schemaVersion":99999999999999999999}'],
  ['BOM на початку тексту (у пам\'яті)', '﻿' + GOLD],
  ['вкладеність 34 рівні', '{"schemaVersion":2,"x":' + '['.repeat(33) + ']'.repeat(33) + '}'],
  ['вкладеність 32 рівні', '{"schemaVersion":2,"coins":1,"gems":1,"levelStars":{},"lastSavedUnixSec":1,"lastKnownUnixSec":1,"x":' + '['.repeat(31) + ']'.repeat(31) + '}'],
];
for (const [name, t] of cases) {
  try { const r = deserialize(t); log('     ' + name + ' -> ПРИЙНЯТО, coins=' + r.d.coins); }
  catch (e) { log('     ' + name + ' -> ' + (e instanceof TooNew ? 'SaveVersionTooNewException' : 'SaveFormatException') + ' (' + e.message + ')'); }
}
{ // одинокий сурогат: UTF8Encoding(false) у .NET і Buffer у Node замінюють його на U+FFFD
  const d = { coins: 1n, gems: 0n, saved: 1n, known: 1n, stars: new Map([['bad\uD800id', 1n]]) };
  const text = serialize(d); const onDisk = Buffer.from(text, 'utf8').toString('utf8');
  log('     id з одиноким сурогатом: текст після запису/читання UTF-8 ' + (onDisk === text ? 'той самий' : 'ІНШИЙ -> canBeLoadedBack: ' + (canBeLoadedBack(text) ? "пропущено" : "RejectedInvalidData до запису") + ''));
}

// ---------- C. Збій на кожній операції Save, усі початкові стани ----------
log('== C. Перебір точок збою Save');
const OLD = serialize({ ...sample(100), saved: 1n, known: 1n }), CUR = serialize({ ...sample(250), saved: 2n, known: 2n });
const variants = { P: { Valid: CUR, Corrupt: 'garbage', Missing: null }, B: { Valid: OLD, Corrupt: '{"schemaVersion":2,"coi', Missing: null } };
let combos = 0; const failsBefore = fails;
for (const pk of Object.keys(variants.P)) for (const bk of Object.keys(variants.B)) for (const tmp of [null, '{"x']) for (const pc of [null, 'older corrupt']) for (const torn of [false, true]) {
  const files = {};
  if (variants.P[pk]) files[P] = variants.P[pk];
  if (variants.B[bk]) files[B] = variants.B[bk];
  if (tmp) files[T] = tmp;
  if (pc) files[P + C] = pc;
  const before = load(new Store(files)); const beforeText = before.d ? serialize(before.d) : null;
  const probe = new Store(files); const nd = sample(999);
  if (save(probe, nd, 5n) !== 'Saved') { check(false, 'еталонний Save'); continue; }
  const newText = serialize(nd); const total = probe.ops;
  for (let f = 1; f <= total; f++) {
    combos++;
    const s = new Store(files); s.failFrom = f; s.torn = torn;
    const r = save(s, sample(999), 5n);
    s.failFrom = Infinity;
    const after = load(s); const afterText = after.d ? serialize(after.d) : null;
    const ok = r === 'Failed' && (afterText === beforeText || afterText === newText) && !(beforeText && after.reset);
    if (!ok) check(false, `P=${pk} B=${bk} tmp=${!!tmp} P.corrupt=${!!pc} torn=${torn} збій на ${f}/${total}: ${r}, після=${after.src}`);
    const s2 = new Store(Object.fromEntries(s.f)); const rr = save(s2, sample(7), 6n); const l2 = load(s2);
    if (rr !== 'Saved' || l2.src !== 'Primary' || l2.d.coins !== 7n) check(false, `відновлення після збою ${pk}/${bk}/${f}`);
  }
}
check(fails === failsBefore, `усі ${combos} комбінацій (P x B x tmp x P.corrupt x torn x точка збою): після збою Load повертає попередній або новий стан, без хибного скидання; наступний Save працює`);

// ---------- D (v2). Стан «недоступний» (BUG-019), карантин (BUG-020), CanSave (BUG-023) ----------
log('== D. v2: недоступні файли, перебір станів Load/Save');
const NEWER = '{"schemaVersion":3,"coins":777}';
const kindNames = ['Valid', 'Corrupt', 'Missing', 'Newer', 'Unavailable'];
const content = (slot, k) => (k === 'Valid' || k === 'Unavailable') ? (slot === P ? CUR : OLD) : k === 'Corrupt' ? 'garbage' : k === 'Newer' ? NEWER : null;
let dCombos = 0; const dFailsBefore = fails;
for (const pk of kindNames) for (const bk of kindNames) for (const copies of [0, 1, 2, 3]) for (const viaExists of [false, true]) {
  const files = {};
  if (pk !== 'Missing') files[P] = content(P, pk);
  if (bk !== 'Missing') files[B] = content(B, bk);
  for (let i = 1; i <= copies; i++) files[cname(P, i)] = 'old damage ' + i;
  const mk = () => {
    const s = new Store(files); const un = new Set();
    if (pk === 'Unavailable') un.add(P); if (bk === 'Unavailable') un.add(B);
    if (viaExists) s.failExists = un; else s.failRead = un; return s;
  };
  dCombos++;
  const tag = `P=${pk} B=${bk} copies=${copies} exists=${viaExists}`;
  const s = mk(); const r = load(s);
  const anyUnavail = pk === 'Unavailable' || bk === 'Unavailable';
  for (const [n, k] of [[P, pk], [B, bk]]) if (k === 'Valid' || k === 'Unavailable' || k === 'Newer') if (s.f.get(n) !== files[n]) check(false, tag + ': Load змінив/перемістив ' + n);
  if (anyUnavail && JSON.stringify([...s.f]) !== JSON.stringify(Object.entries(files))) check(false, tag + ': Load змінив носій при недоступному файлі');
  if (pk === 'Unavailable' && (r.src !== 'Unavailable' || r.canSave)) check(false, tag + ': очікувано Unavailable/CanSave=false, є ' + r.src);
  if (pk === 'Valid' && bk === 'Unavailable' && (r.src !== 'Primary' || r.canSave)) check(false, tag + ': очікувано Primary/CanSave=false');
  if ((pk === 'Corrupt' || pk === 'Missing') && bk === 'Unavailable' && r.src !== 'Unavailable') check(false, tag + ': очікувано Unavailable');
  if (anyUnavail && r.reset) check(false, tag + ': хибне ProgressReset');
  for (let i = 1; i <= Math.min(copies, 2); i++) if (s.f.get(cname(P, i)) !== 'old damage ' + i) check(false, tag + ': затерто ' + cname(P, i));
  const before2 = JSON.stringify([...s.f]); const w = save(s, sample(5), 9n);
  if (r.canSave !== (w === 'Saved')) check(false, tag + `: CanSave=${r.canSave}, а Save=${w}`);
  if (anyUnavail && (!(w === 'Failed' || (w === 'RejectedNewerSave' && (pk === 'Newer' || bk === 'Newer'))) || JSON.stringify([...s.f]) !== before2)) check(false, tag + ': Save при недоступному файлі ' + w);
}
check(fails === dFailsBefore, `усі ${dCombos} станів (P x B з 5 видів x 0-3 відкладених копій x збій Exists/ReadText): недоступні/справні/новіші файли не чіпаються, таблиця станів як заявлено, без хибного скидання, CanSave == (Save вдався), Save при недоступному = Failed (або RejectedNewerSave, якщо інший файл новішої версії) без змін, копії .corrupt і .corrupt.2 цілі`);

{ // Початковий сценарій BUG-019
  const s = new Store({ [P]: CUR, [B]: OLD }); s.failRead = new Set([P, B]);
  const r = load(s); const blank = { coins: 0n, gems: 0n, saved: 0n, known: 0n, stars: new Map() };
  const w1 = save(s, blank, 10n), w2 = save(s, blank, 11n); s.failRead.clear(); const l = load(s);
  check(r.src === 'Unavailable' && w1 === 'Failed' && w2 === 'Failed' && l.src === 'Primary' && l.d.coins === 250n,
    `сценарій BUG-019: обидва не читаються -> ${r.src}; 2 збереження -> ${w1}/${w2}; доступ повернувся -> ${l.src}, coins=${l.d.coins}`);
}
{ // Лише основний не читається: без мовчазного відкату
  const s = new Store({ [P]: CUR, [B]: OLD }); s.failRead = new Set([P]);
  const r = load(s);
  check(r.src === 'Unavailable' && !r.d && !r.canSave, `лише основний не читається -> ${r.src}, дані резервної копії не видано, CanSave=${r.canSave}`);
}
{ // Залишковий ризик інтеграції
  const s = new Store({ [P]: CUR, [B]: OLD }); s.failRead = new Set([P, B]);
  load(s); const blank = { coins: 0n, gems: 0n, saved: 0n, known: 0n, stars: new Map() };
  s.failRead.clear();
  const w1 = save(s, blank, 10n), w2 = save(s, blank, 11n);
  const pc = deserialize(s.f.get(P)).d.coins, bc = deserialize(s.f.get(B)).d.coins;
  log(`     РИЗИК (BUG-025): Load=Unavailable, гра ігнорує CanSave і ПІСЛЯ відновлення доступу двічі зберігає Data (чистий профіль) з того результату -> ${w1}/${w2}; основний coins=${pc}, резервний coins=${bc}; справжній прогрес (250/100) зник`);
}
{ // Пошкоджений резервний при справному основному
  const s = new Store({ [P]: CUR, [B]: 'garbage-bak' }); const r = load(s); const w = save(s, sample(5), 9n);
  log(`     пошкоджений .bak при справному основному: Load=${r.src} (CorruptFilesFound=${r.corrupt}); Save=${w}; файли: ${[...s.f.keys()].join(', ')} -> текст "garbage-bak" видалено без карантину (BUG-026)`);
}
log('');
log(fails === 0 ? 'ПІДСУМОК v2: A-D без порушень; залишкові ризики — BUG-025, BUG-026' : 'ПІДСУМОК v2: порушень ' + fails);
console.log(out.join('\n'));

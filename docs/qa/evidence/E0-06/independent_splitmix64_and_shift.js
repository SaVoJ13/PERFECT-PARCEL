const M=(1n<<64n)-1n;
class R{constructor(s){this.st=BigInt(s)&M}
 u64(){this.st=(this.st+0x9E3779B97F4A7C15n)&M;let z=this.st;
  z=((z^(z>>30n))*0xBF58476D1CE4E5B9n)&M; z=((z^(z>>27n))*0x94D049BB133111EBn)&M; return z^(z>>31n)}
 ni(a,b){const rng=BigInt(b)-BigInt(a);const th=((1n<<64n)-rng)%rng;let v;do{v=this.u64()}while(v<th);return Number(BigInt(a)+v%rng)}}
const hx=v=>'0x'+v.toString(16).toUpperCase().padStart(16,'0');
const take=(s,n)=>{const r=new R(s);return Array.from({length:n},()=>hx(r.u64()))};
console.log(take(0,5));console.log(take(1234567,5));console.log(take(M,5));
let r=new R(0);for(let i=0;i<5;i++)r.u64();console.log(hx(r.st));
r=new R(42);console.log(Array.from({length:12},()=>r.ni(0,10)).join(','));
r=new R(42);console.log(Array.from({length:8},()=>r.ni(-5,5)).join(','));
r=new R(7);console.log(Array.from({length:8},()=>r.ni(0,1000000007)).join(','));
r=new R(7);console.log(Array.from({length:6},()=>r.ni(-2147483648,2147483647)).join(','));
r=new R(5);console.log(r.ni(9,10));
// banker's rounding like Math.Round(double)
function bround(x){const f=Math.floor(x);const d=x-f;if(d>0.5)return f+1;if(d<0.5)return f;return (f%2===0)?f:f+1}
const SUB=1000000;
class Sim{constructor(tps,mc,dur,bonus,seed,goal){Object.assign(this,{tps,mc,dur,bonus,goal});this.r=new R(seed);this.end=dur*tps;this.pend=0;this.el=0;this.state='Intro';this.cu=false}
 tr(a,b){if(this.state!==a)return false;this.state=b;return true}
 start(){return this.tr('Intro','Running')} pause(){return this.tr('Running','Paused')} resume(){return this.tr('Paused','Running')}
 cont(){if(this.state!=='Lost'||this.cu||this.bonus*this.tps<=0)return false;this.cu=true;this.end+=this.bonus*this.tps;this.state='Running';return true}
 tick(){if(this.state!=='Running')return false;this.el++;if(this.el>=this.end){this.pend=0;this.state='Resolving';this.state=this.goal(this)?'Won':'Lost'}return true}
 adv(d){if(this.state!=='Running')return 0;const mt=this.mc;const ex=d*this.tps;let due=mt+1;
  if(ex<due){this.pend+=bround(ex*SUB);due=Math.floor(this.pend/SUB);this.pend%=SUB}
  if(due>mt){due=mt;this.pend=0} let e=0;while(e<due&&this.tick())e++;return e}
 snap(){return [this.state,this.el,this.end-this.el,this.pend,this.cu,hx(this.r.st)].join(' ')}}
const rg=s=>s.r.ni(0,2)===0;
function run(ss,sc,steps=3000){const sh=new Sim(60,20,5,2,ss,rg);const scr=new R(sc);const out=[];
 for(let i=0;i<steps;i++){const c=scr.ni(0,12);
  if(c===0)sh.start();else if(c===1)sh.pause();else if(c===2)sh.resume();else if(c===3)sh.cont();else if(c===4)sh.tick();else if(c===5)sh.r.u64();else sh.adv(scr.ni(0,4000)/100000);
  out.push(sh.snap())}return out}
console.log('golden scenario', run(2026,7)[2999]);
console.log('5/9 vs 6/9', run(5,9)[2999], '|', run(6,9)[2999]);
let o='';for(let i=0;i<16;i++){const sh=new Sim(60,20,5,2,i+1,rg);sh.start();while(sh.tick());o+=sh.state==='Won'?'W':'L'}console.log(o);
const s1=run(1,100);console.log(new Set(s1.map(x=>x.split(' ')[0])), s1[2999]);
const f=Math.fround(1/60);console.log('float', f*60*SUB);
let a=new Sim(50,600,20,30,1,()=>true);a.start();for(let i=0;i<600;i++)a.adv(1/60);
let b=new Sim(50,600,20,30,1,()=>true);b.start();b.adv(10);console.log('tps50 600x1/60:',a.el,a.pend,' 1x10:',b.el,b.pend);
a=new Sim(60,600,20,30,1,()=>true);a.start();for(let i=0;i<1000;i++)a.adv(0.01);
console.log('tps60 1000x0.01:',a.el,a.pend);
a=new Sim(60,600,60,30,1,()=>true);a.start();for(let i=0;i<3000;i++)a.adv(1/144);console.log('144Hz 3000 frames (20.833s):',a.el,a.pend, 'exact ticks', 3000/144*60);

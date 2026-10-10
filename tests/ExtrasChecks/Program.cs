using Chronicles;
using ChroniclesLauncher;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
var failed=0;
void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Require(bool value){if(!value)throw new Exception("Assertion failed");}
Check("Interview selection press must be released before skipping",()=>{
 Extras.BeginMovie(2); Require((Extras.FilterMovieButtons(0x40)&0x60)==0);
 Require((Extras.FilterMovieButtons(0x60)&0x60)==0);
 Require(Extras.FilterMovieButtons(0x100)==0x100);
 Require(Extras.FilterMovieButtons(0x40)==0x40);
 Extras.BeginMovie(2);Require(Extras.FilterMovieButtons(0x20)==0);
 Extras.BeginMovie(0);Require(Extras.FilterMovieButtons(0x40)==0x40);
});
Check("Gallery pumps one frame preserving CPU registers",()=>{
 Interrupts.Turbo=true;var m=new PSMemory();var c=new CpuContext{A0=17,V0=23,SP=0x801ff000,RA=0x80109dcc};
 var count=Interrupts.VBlankCount;Extras.PresentGallery(c,m);
 Require(Interrupts.VBlankCount-count==1);Require(c.A0==17&&c.V0==23&&c.SP==0x801ff000&&c.RA==0x80109dcc);
});
var root=Path.Combine(Path.GetTempPath(),"chronicles-cards-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try {
 byte[] Card(byte marker){var b=new byte[131072];b[0]=77;b[1]=67;b[128]=marker;return b;}
 var source=Path.Combine(root,"portable");var data=Path.Combine(root,"data");Directory.CreateDirectory(source);
 var original=Card(1);File.WriteAllBytes(Path.Combine(source,"carda.sav"),original);
 Check("Missing portable cards are adopted without overwriting existing progress",()=>{
  MemoryCardStorage.AdoptMissingCards(data,source);Require(File.ReadAllBytes(Path.Combine(data,"carda.sav")).SequenceEqual(original));
  File.WriteAllBytes(Path.Combine(source,"carda.sav"),Card(2));MemoryCardStorage.AdoptMissingCards(data,source);
  Require(File.ReadAllBytes(Path.Combine(data,"carda.sav")).SequenceEqual(original));
 });
 Check("Explicit import preserves backup and rejects invalid files",()=>{
  Directory.CreateDirectory(data);var dest=Path.Combine(data,"carda.sav");File.WriteAllBytes(dest,original);
  var incoming=Path.Combine(source,"carda.sav");File.WriteAllBytes(incoming,Card(2));MemoryCardStorage.Import(incoming,dest);
  Require(File.ReadAllBytes(dest).SequenceEqual(Card(2)));Require(Directory.GetFiles(data,"carda.sav.*.bak").Any(p=>File.ReadAllBytes(p).SequenceEqual(original)));
  File.WriteAllText(incoming,"invalid");try{MemoryCardStorage.Import(incoming,dest);throw new Exception("Accepted invalid card");}catch(InvalidDataException){}
  Require(File.ReadAllBytes(dest).SequenceEqual(Card(2)));
 });
}finally{Directory.Delete(root,true);}
return failed==0?0:1;

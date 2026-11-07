using WinCC.Layout;
using System.Diagnostics;
const int w=1000,h=600,lx=180,ly=160,lw=640,lh=280;
var background=new byte[w*h*4];
for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
 int i=(y*w+x)*4;
 background[i]=(byte)(80+100d*y/h); background[i+1]=(byte)(65+100d*x/w); background[i+2]=(byte)(180-90d*x/w);
 if(x%80<4 || y%80<4) background[i]=background[i+1]=background[i+2]=230;
 if(Math.Abs(x-y-100)<10) {background[i]=30;background[i+1]=190;background[i+2]=245;}
}
int sw=800,sh=350;var source=new byte[sw*sh*4];
for(int y=0;y<sh;y++)for(int x=0;x<sw;x++) Array.Copy(background,((ly-lh/8+y)*w+lx-lw/8+x)*4,source,(y*sw+x)*4,4);
var timer=Stopwatch.StartNew();var glass=GlassOptics.Render(source,sw,sh,lw,lh);timer.Stop();
var composite=(byte[])background.Clone();
for(int y=0;y<lh;y++)for(int x=0;x<lw;x++) {
 int si=(y*lw+x)*4,di=((y+ly)*w+x+lx)*4;double a=glass[si+3]/255d;
 for(int c=0;c<3;c++)composite[di+c]=(byte)(glass[si+c]*a+composite[di+c]*(1-a));
}
Directory.CreateDirectory("artifacts");
using(var stream=File.Create("artifacts/glass-optics.ppm")) {
 stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n"));
 for(int i=0;i<composite.Length;i+=4) {stream.WriteByte(composite[i+2]);stream.WriteByte(composite[i+1]);stream.WriteByte(composite[i]);}
}
Console.WriteLine($"Optical render {lw}x{lh}: {timer.ElapsedMilliseconds} ms. Synthetic source; not a Windows screenshot.");

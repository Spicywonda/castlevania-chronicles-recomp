using Chronicles;
using Recompiled;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;

static class ParallaxChecks
{
    public static void Run()
    {
        int cases = 0;
        foreach (int camera in new[] { 0, 20, 43, 44, 64, 100, 128, 400, 750, 768 })
        foreach (int divisor in new[] { 3, 6 })
        {
            var m = new PSMemory();
            const uint bg=0x800593ec, map=0x80012000, tiles=0x80013000, table=0x80011000, ot=0x80015000, arena=0x80016000;
            m.WriteU8(0x80059286,15); m.WriteU16(0x80059248,(ushort)camera);
            m.WriteU32(0x8010ac44,0x8010ac54);m.WriteU16(0x8010ac56,32);
            m.WriteU16(bg+4,128);m.WriteU16(bg+6,32);m.WriteU16(bg+8,256);m.WriteU16(bg+10,32);
            m.WriteU16(bg+12,(ushort)(camera/divisor));m.WriteU32(bg+16,0x808080);m.WriteU32(bg+20,map);
            m.WriteU16(bg+24,128);m.WriteU16(bg+28,4096);m.WriteU16(bg+30,4096);
            m.WriteU8(map,32);m.WriteU8(map+1,32);m.WriteU16(map+2,24);m.WriteU16(map+4,1);m.WriteU32(map+8,tiles);m.WriteU32(map+12,0x80014000);
            for(uint i=0;i<24;i++) m.WriteU16(0x80014000+i*2,(ushort)i);
            for(uint i=0;i<24;i++) {m.WriteU16(tiles+i*8,(ushort)(i+1));m.WriteU16(tiles+i*8+6,4);}
            m.WriteU32(table+4,ot);m.WriteU32(ot,0xffffff);m.WriteU32(0x80097ca8,arena);
            var c=new CpuContext{A0=bg,A1=table,A2=0,RA=0x80101734,SP=0x801ff000};
            Widescreen.Enabled=true;Display.WideAspect=16f/9f;
            using(WorldViewport.Begin(m)) CastlevaniaChronicles_main.GsSortFastBg(c,m);
            var covered=new bool[342];int nodes=0;
            for(uint p=m.ReadU32(ot)&0xffffff;p!=0xffffff;p=m.ReadU32(p)&0xffffff)
            {
                if(++nodes>64)throw new Exception("Invalid parallax chain");
                int x, width;
                if ((m.ReadU32(p+4)>>24 & 0xfc)==0x2c)
                {
                    x=Math.Min((short)m.ReadU16(p+8),(short)m.ReadU16(p+16));
                    width=Math.Abs((short)m.ReadU16(p+16)-(short)m.ReadU16(p+8));
                }
                else if ((m.ReadU32(p+8)>>24 & 0xfc)==0x64)
                {
                    x=(short)m.ReadU16(p+12);width=m.ReadU16(p+20);
                }
                else throw new Exception("Unexpected parallax primitive");
                for(int q=Math.Max(-43,x);q<Math.Min(299,x+width);q++) covered[q+43]=true;
            }
            if(covered.Any(x=>!x))throw new Exception($"Parallax is cut: camera={camera}, scroll={camera/divisor}, missing={covered.Count(x=>!x)} pixels");
            if(m.ReadU16(bg+4)!=128||m.ReadU16(bg+8)!=256||m.ReadU16(bg+12)!=camera/divisor)throw new Exception("Parallax state not restored");
            cases++;
        }
        Console.WriteLine($"PASS {cases} real parallax draws cover every widescreen pixel at camera boundaries and slow scroll positions");
    }
}

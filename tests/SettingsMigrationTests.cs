using System;
using System.IO;
using System.Linq;
using System.Text;
using BigPictureMonitores;

class SettingsMigrationTests {
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    [STAThread] static void Main() {
        string source=Path.Combine(Path.GetTempPath(),"BigPictureControle-MigrationTest-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);Files.Initialize(true);
        try {
            Assert(Files.Root!=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BigPictureControle"),"Preview used live data");
            File.WriteAllText(Path.Combine(source,"principal.txt"),@"\\.\DISPLAY4");
            File.WriteAllText(Path.Combine(source,"audio-nome.txt"),"Saída de teste",Encoding.UTF8);
            File.WriteAllText(Path.Combine(source,"audio-saida.txt"),"fake-device-id");
            File.WriteAllText(Path.Combine(source,"audio-automatico.txt"),"1");
            File.WriteAllText(Path.Combine(source,"barra-automatica.txt"),"0");
            foreach(string name in new[]{"monitores-originais.bin","audio-originais.bin","barra-restaurar.bin","app.log","app-status.txt","cursor-oculto.flag","arbitrary.exe"})File.WriteAllText(Path.Combine(source,name),"do not import");
            Files.ImportSettings(source);
            Assert(Directory.GetFiles(Files.Root).Length==5,"Imported runtime or unrelated files");
            Assert(File.ReadAllText(Files.At("audio-nome.txt"),Encoding.UTF8)=="Saída de teste","Unicode preference lost");
            Assert(File.ReadAllText(Files.At("audio-saida.txt"))=="fake-device-id","Output preference lost");
            File.WriteAllText(Path.Combine(source,"principal.txt"),@"\\.\DISPLAY2");
            Files.ImportSettings(source);
            Assert(File.ReadAllText(Files.At("principal.txt"))==@"\\.\DISPLAY4","Existing preference overwritten");
            File.Delete(Files.At("barra-automatica.txt"));File.WriteAllText(Path.Combine(source,"barra-automatica.txt"),"unexpected");
            Files.ImportSettings(source);
            Assert(!File.Exists(Files.At("barra-automatica.txt")),"Invalid option imported");
            Console.WriteLine("PASS: isolated preview data, preference allowlist, Unicode, preserved existing preferences, rejected invalid switches.");
        }finally{Files.CleanPreview();Directory.Delete(source,true);}
    }
}

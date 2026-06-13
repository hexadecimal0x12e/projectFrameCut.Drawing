using BenchmarkDotNet.Attributes;
using projectFrameCut.Drawing.Text.Entry;
using projectFrameCut.Drawing.Text.FontHelper;
using projectFrameCut.Drawing.Text.Typology;

namespace projectFrameCut.Drawing.Benchmarks.Benchmarks.Text;

[MemoryDiagnoser]
public class LineBreakBenchmarks : BenchmarkBase
{
    private FontFace _font = null!;
    private string _text = null!;
    private TextEntry _entry = null!;

    public enum TextLength { Short, Long }

    public enum TextCategory { Latin, CJK }

    [Params(TextLength.Short, TextLength.Long)]
    public TextLength Length { get; set; }

    [Params(TextCategory.Latin, TextCategory.CJK)]
    public TextCategory Category { get; set; }

    [Params(0.5f, 0.2f)]
    public float TargetWidth { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        NormalTypesettingEngine.DebugDumpAdvance = false;

        if (Category == TextCategory.CJK)
        {
            var cjkFontPath = GetCjkFontPath();
            _font = FontFace.AutoLoad(cjkFontPath)[0];
        }
        else
        {
            var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "Fonts", "OpenSans-Regular.ttf");
            _font = FontFace.Load(fontPath);
        }

        _text = (Category, Length) switch
        {
            (TextCategory.Latin, TextLength.Short) => "Hello, World! This is a short sentence.",
            (TextCategory.Latin, TextLength.Long) => "The quick brown fox jumps over the lazy dog. " +
                               "Pack my box with five dozen liquor jugs. " +
                               "How vexingly quick daft zebras jump! " +
                               "The five boxing wizards jump quickly. " +
                               "Sphinx of black quartz, judge my vow. " +
                               "When zombies arrive, quickly fax each judge. " +
                               "The jay, pig, fox, zebra, and my wolves quack! " +
                               "Blowzy night-frumps vex'd Jack Q.",
            (TextCategory.CJK, TextLength.Short) => "你好，世界！这是一个简短的句子。",
            (TextCategory.CJK, TextLength.Long) => "春风又绿江南岸，明月何时照我还。人生自古谁无死，留取丹心照汗青。" +
                        "落霞与孤鹜齐飞，秋水共长天一色。问君能有几多愁，恰似一江春水向东流。" +
                        "山重水复疑无路，柳暗花明又一村。不畏浮云遮望眼，自缘身在最高层。" +
                        "海内存知己，天涯若比邻。但愿人长久，千里共婵娟。" +
                        "壮志饥餐胡虏肉，笑谈渴饮匈奴血。待从头、收拾旧山河，朝天阙。" +
                        "醉卧沙场君莫笑，古来征战几人回。黄沙百战穿金甲，不破楼兰终不还。" +
                        "纸上得来终觉浅，绝知此事要躬行。问渠那得清如许，为有源头活水来。" +
                        "昨夜西风凋碧树，独上高楼，望尽天涯路。衣带渐宽终不悔，为伊消得人憔悴。",
            _ => ""
        };

        _entry = new TextEntry
        {
            Text = _text,
            FontName = Category == TextCategory.CJK ? "CJK-Font" : "OpenSans",
            FontSize = 0.1f
        };
    }

    [Benchmark(Description = "BreakLine (TextEntry)")]
    public string BreakLineEntry() => LineBreakHandler.BreakLine(_entry, _font, TargetWidth);
}

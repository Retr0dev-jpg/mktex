namespace MkTeX.Parsing;

public enum AtomType
{
    Ord,
    Op,
    Bin,
    Rel,
    Open,
    Close,
    Punct,
    Inner
}

internal readonly record struct Symbol(int CodePoint, AtomType Type, bool Limits = false);

/// <summary>LaTeX, AMS and common package symbol commands.</summary>
internal static class Symbols
{
    // "class: name hex name hex ..." groups; "op+" marks large operators that take limits in display style.
    private const string Table = """
        ord: alpha 3B1 beta 3B2 gamma 3B3 delta 3B4 epsilon 3F5 varepsilon 3B5 zeta 3B6 eta 3B7 theta 3B8
        vartheta 3D1 iota 3B9 kappa 3BA varkappa 3F0 lambda 3BB mu 3BC nu 3BD xi 3BE omicron 3BF pi 3C0
        varpi 3D6 rho 3C1 varrho 3F1 sigma 3C3 varsigma 3C2 tau 3C4 upsilon 3C5 phi 3D5 varphi 3C6 chi 3C7
        psi 3C8 omega 3C9 digamma 3DD Gamma 393 Delta 394 Theta 398 Lambda 39B Xi 39E Pi 3A0 Sigma 3A3
        Upsilon 3A5 Phi 3A6 Psi 3A8 Omega 3A9 Alpha 391 Beta 392 Epsilon 395 Zeta 396 Eta 397 Iota 399
        Kappa 39A Mu 39C Nu 39D Omicron 39F Rho 3A1 Tau 3A4 Chi 3A7 varGamma 1D6E4 varDelta 1D6E5
        varTheta 1D6E9 varLambda 1D6EC varXi 1D6EF varPi 1D6F1 varSigma 1D6F4 varUpsilon 1D6F6 varPhi 1D6F7
        varPsi 1D6F9 varOmega 1D6FA aleph 2135 beth 2136 gimel 2137 daleth 2138 hbar 210F hslash 210F
        ell 2113 wp 2118 Re 211C Im 2111 partial 2202 infty 221E prime 2032 backprime 2035 emptyset 2205
        varnothing 2300 nabla 2207 surd 221A top 22A4 bot 22A5 angle 2220 measuredangle 2221
        sphericalangle 2222 triangle 25B3 forall 2200 exists 2203 nexists 2204 neg AC lnot AC flat 266D
        natural 266E sharp 266F clubsuit 2663 diamondsuit 2662 heartsuit 2661 spadesuit 2660 Box 25A1
        Diamond 25C7 mho 2127 eth F0 complement 2201 Finv 2132 Game 2141 imath 1D6A4 jmath 1D6A5
        checkmark 2713 maltese 2720 circledR AE circledS 24C8 blacksquare 25A0 square 25A1 lozenge 25CA
        blacklozenge 29EB bigstar 2605 blacktriangle 25B4 blacktriangledown 25BE triangledown 25BF
        vartriangle 25B5 diagup 2571 diagdown 2572 degree B0 S A7 P B6 copyright A9 pounds A3 yen A5
        euro 20AC vdots 22EE infinity 221E Bbbk 1D55C hbars 210F textbackslash 5C backslash 5C
        vert 7C Vert 2016 | 2016 dagger 2020 ddagger 2021 dag 2020 ddag 2021 colon 3A
        bin: pm B1 mp 2213 times D7 div F7 cdot 22C5 ast 2217 star 22C6 circ 2218 bullet 2219 oplus 2295
        ominus 2296 otimes 2297 oslash 2298 odot 2299 cup 222A cap 2229 sqcup 2294 sqcap 2293 vee 2228
        lor 2228 wedge 2227 land 2227 setminus 2216 smallsetminus 2216 wr 2240 diamond 22C4
        bigtriangleup 25B3 bigtriangledown 25BD triangleleft 25C1 triangleright 25B7 lhd 22B2 rhd 22B3
        unlhd 22B4 unrhd 22B5 uplus 228E amalg 2A3F dotplus 2214 centerdot 22C5 ltimes 22C9 rtimes 22CA
        leftthreetimes 22CB rightthreetimes 22CC curlywedge 22CF curlyvee 22CE barwedge 22BC veebar 22BB
        doublebarwedge 2A5E boxminus 229F boxtimes 22A0 boxdot 22A1 boxplus 229E divideontimes 22C7
        circledast 229B circledcirc 229A circleddash 229D intercal 22BA Cap 22D2 Cup 22D3 bigcirc 25EF
        gtrdot 22D7 lessdot 22D6 minuso 29B5 cdotp 22C5
        rel: leq 2264 le 2264 geq 2265 ge 2265 neq 2260 ne 2260 equiv 2261 approx 2248 approxeq 224A
        sim 223C simeq 2243 cong 2245 asymp 224D doteq 2250 propto 221D prec 227A succ 227B preceq 2AAF
        succeq 2AB0 ll 226A gg 226B lll 22D8 ggg 22D9 subset 2282 supset 2283 subseteq 2286 supseteq 2287
        subsetneq 228A supsetneq 228B nsubseteq 2288 nsupseteq 2289 sqsubset 228F sqsupset 2290
        sqsubseteq 2291 sqsupseteq 2292 in 2208 ni 220B owns 220B notin 2209 vdash 22A2 dashv 22A3
        models 22A7 vDash 22A8 Vdash 22A9 Vvdash 22AA mid 2223 nmid 2224 parallel 2225 nparallel 2226
        perp 27C2 smile 2323 frown 2322 bowtie 22C8 Join 22C8 leftarrow 2190 gets 2190 rightarrow 2192
        to 2192 uparrow 2191 downarrow 2193 leftrightarrow 2194 updownarrow 2195 Leftarrow 21D0
        Rightarrow 21D2 Uparrow 21D1 Downarrow 21D3 Leftrightarrow 21D4 Updownarrow 21D5 mapsto 21A6
        longmapsto 27FC longleftarrow 27F5 longrightarrow 27F6 longleftrightarrow 27F7 Longleftarrow 27F8
        Longrightarrow 27F9 Longleftrightarrow 27FA implies 27F9 impliedby 27F8 iff 27FA hookleftarrow 21A9
        hookrightarrow 21AA leftharpoonup 21BC leftharpoondown 21BD rightharpoonup 21C0
        rightharpoondown 21C1 rightleftharpoons 21CC leftrightharpoons 21CB nearrow 2197 searrow 2198
        swarrow 2199 nwarrow 2196 leadsto 21DD rightsquigarrow 21DD leftrightsquigarrow 21AD
        twoheadrightarrow 21A0 twoheadleftarrow 219E leftleftarrows 21C7 rightrightarrows 21C9
        leftrightarrows 21C6 rightleftarrows 21C4 upuparrows 21C8 downdownarrows 21CA curvearrowleft 21B6
        curvearrowright 21B7 circlearrowleft 21BA circlearrowright 21BB Lsh 21B0 Rsh 21B1
        looparrowleft 21AB looparrowright 21AC leftarrowtail 21A2 rightarrowtail 21A3 Lleftarrow 21DA
        Rrightarrow 21DB nleftarrow 219A nrightarrow 219B nLeftarrow 21CD nRightarrow 21CF
        nleftrightarrow 21AE nLeftrightarrow 21CE multimap 22B8 upharpoonleft 21BF upharpoonright 21BE
        downharpoonleft 21C3 downharpoonright 21C2 restriction 21BE leqq 2266 geqq 2267 leqslant 2A7D
        geqslant 2A7E eqslantless 2A95 eqslantgtr 2A96 lesssim 2272 gtrsim 2273 lessapprox 2A85
        gtrapprox 2A86 lessgtr 2276 gtrless 2277 lesseqgtr 22DA gtreqless 22DB lesseqqgtr 2A8B
        gtreqqless 2A8C nless 226E ngtr 226F nleq 2270 ngeq 2271 lneq 2A87 gneq 2A88 lneqq 2268
        gneqq 2269 lnsim 22E6 gnsim 22E7 lnapprox 2A89 gnapprox 2A8A nsim 2241 ncong 2247 thicksim 223C
        thickapprox 2248 backsim 223D backsimeq 22CD triangleq 225C eqcirc 2256 circeq 2257 bumpeq 224F
        Bumpeq 224E doteqdot 2251 Doteq 2251 fallingdotseq 2252 risingdotseq 2253 coloneqq 2254
        eqqcolon 2255 coloneq 2254 Coloneqq 2A74 precsim 227E succsim 227F precapprox 2AB7
        succapprox 2AB8 preccurlyeq 227C succcurlyeq 227D curlyeqprec 22DE curlyeqsucc 22DF nprec 2280
        nsucc 2281 npreceq 22E0 nsucceq 22E1 precneqq 2AB5 succneqq 2AB6 precnsim 22E8 succnsim 22E9
        precnapprox 2AB9 succnapprox 2ABA Subset 22D0 Supset 22D1 subseteqq 2AC5 supseteqq 2AC6
        subsetneqq 2ACB supsetneqq 2ACC nsubseteqq 2288 nsupseteqq 2289 vartriangleleft 22B2
        vartriangleright 22B3 trianglelefteq 22B4 trianglerighteq 22B5 ntriangleleft 22EA
        ntriangleright 22EB ntrianglelefteq 22EC ntrianglerighteq 22ED between 226C pitchfork 22D4
        shortmid 2223 shortparallel 2225 nshortmid 2224 nshortparallel 2226 therefore 2234 because 2235
        varpropto 221D blacktriangleleft 25C2 blacktriangleright 25B8 backepsilon 3F6 smallsmile 2323
        smallfrown 2322 nvdash 22AC nvDash 22AD nVdash 22AE nVDash 22AF sqsubsetneq 22E4 sqsupsetneq 22E5
        varsubsetneq 228A varsupsetneq 228B eqsim 2242 neqsim 2242 approxcolon 2248 dashrightarrow 21E2
        dashleftarrow 21E0 Longmapsto 27FE leftrightarroweq 21D4 origof 22B6 imageof 22B7 lrcorner 231F
        perp 27C2 propto 221D equal 3D
        op+: sum 2211 prod 220F coprod 2210 bigcup 22C3 bigcap 22C2 bigvee 22C1 bigwedge 22C0 bigoplus 2A01
        bigotimes 2A02 bigodot 2A00 biguplus 2A04 bigsqcup 2A06 bigsqcap 2A05 bigtimes 2A09
        op: int 222B iint 222C iiint 222D iiiint 2A0C oint 222E oiint 222F oiiint 2230 intop 222B
        smallint 222B varointclockwise 2232 ointctrclockwise 2233 fint 2A0F sqint 2A16
        open: lbrace 7B langle 27E8 lfloor 230A lceil 2308 lvert 7C lVert 2016 lgroup 27EE lmoustache 23B0
        ulcorner 231C llcorner 231E lbrack 5B llbracket 27E6 lparen 28 lBrace 2983 lAngle 27EA
        close: rbrace 7D rangle 27E9 rfloor 230B rceil 2309 rvert 7C rVert 2016 rgroup 27EF rmoustache 23B1
        urcorner 231D rbrack 5D rrbracket 27E7 rparen 29 rBrace 2984 rAngle 27EB
        inner: ldots 2026 cdots 22EF ddots 22F1 iddots 22F0 dots 2026 dotsc 2026 dotsb 22EF dotsm 22EF
        dotsi 22EF dotso 2026 mathellipsis 2026 adots 22F0
        punct: ldotp 2E
        """;

    private static readonly Dictionary<string, Symbol> Commands = Build();

    private static readonly Dictionary<int, AtomType> CharTypes = BuildCharTypes();

    public static readonly Dictionary<string, (string Text, bool Limits)> Operators = new(StringComparer.Ordinal)
    {
        ["arccos"] = ("arccos", false), ["arcsin"] = ("arcsin", false), ["arctan"] = ("arctan", false),
        ["arg"] = ("arg", false), ["cos"] = ("cos", false), ["cosh"] = ("cosh", false), ["cot"] = ("cot", false),
        ["coth"] = ("coth", false), ["csc"] = ("csc", false), ["deg"] = ("deg", false), ["det"] = ("det", true),
        ["dim"] = ("dim", false), ["exp"] = ("exp", false), ["gcd"] = ("gcd", true), ["hom"] = ("hom", false),
        ["inf"] = ("inf", true), ["ker"] = ("ker", false), ["lg"] = ("lg", false), ["lim"] = ("lim", true),
        ["liminf"] = ("lim inf", true), ["limsup"] = ("lim sup", true), ["ln"] = ("ln", false),
        ["log"] = ("log", false), ["max"] = ("max", true), ["min"] = ("min", true), ["Pr"] = ("Pr", true),
        ["sec"] = ("sec", false), ["sin"] = ("sin", false), ["sinh"] = ("sinh", false), ["sup"] = ("sup", true),
        ["tan"] = ("tan", false), ["tanh"] = ("tanh", false), ["injlim"] = ("inj lim", true),
        ["projlim"] = ("proj lim", true), ["argmax"] = ("arg max", true), ["argmin"] = ("arg min", true),
        ["sgn"] = ("sgn", false), ["tr"] = ("tr", false), ["Tr"] = ("Tr", false), ["rank"] = ("rank", false),
        ["arcctg"] = ("arcctg", false), ["arctg"] = ("arctg", false), ["ch"] = ("ch", false),
        ["cosec"] = ("cosec", false), ["cotg"] = ("cotg", false), ["ctg"] = ("ctg", false), ["cth"] = ("cth", false),
        ["sh"] = ("sh", false), ["tg"] = ("tg", false), ["th"] = ("th", false), ["erf"] = ("erf", false),
        ["Var"] = ("Var", false), ["Cov"] = ("Cov", false), ["diag"] = ("diag", false), ["span"] = ("span", false)
    };

    public static readonly Dictionary<string, (int Accent, bool Wide, bool Under, bool Stretchy)> Accents = new(StringComparer.Ordinal)
    {
        ["hat"] = (0x302, false, false, false), ["check"] = (0x30C, false, false, false),
        ["tilde"] = (0x303, false, false, false), ["acute"] = (0x301, false, false, false),
        ["grave"] = (0x300, false, false, false), ["dot"] = (0x307, false, false, false),
        ["ddot"] = (0x308, false, false, false), ["dddot"] = (0x20DB, false, false, false),
        ["ddddot"] = (0x20DC, false, false, false), ["breve"] = (0x306, false, false, false),
        ["bar"] = (0x304, false, false, false), ["vec"] = (0x20D7, false, false, false),
        ["mathring"] = (0x30A, false, false, false), ["widehat"] = (0x302, true, false, false),
        ["widetilde"] = (0x303, true, false, false), ["widecheck"] = (0x30C, true, false, false),
        ["widebar"] = (0x305, true, false, false), ["utilde"] = (0x330, true, true, false),
        ["overrightarrow"] = (0x2192, true, false, true), ["overleftarrow"] = (0x2190, true, false, true),
        ["overleftrightarrow"] = (0x2194, true, false, true), ["underrightarrow"] = (0x2192, true, true, true),
        ["underleftarrow"] = (0x2190, true, true, true), ["underleftrightarrow"] = (0x2194, true, true, true),
        ["overgroup"] = (0x23E0, true, false, true), ["undergroup"] = (0x23E1, true, true, true),
        ["overlinesegment"] = (0x20E9, true, false, true), ["Overrightarrow"] = (0x21D2, true, false, true),
        ["overrightharpoon"] = (0x21C0, true, false, true), ["overleftharpoon"] = (0x21BC, true, false, true)
    };

    public static readonly Dictionary<string, int> Negations = new(StringComparer.Ordinal)
    {
        ["="] = 0x2260, ["<"] = 0x226E, [">"] = 0x226F, ["\\in"] = 0x2209, ["\\equiv"] = 0x2262,
        ["\\subset"] = 0x2284, ["\\supset"] = 0x2285, ["\\subseteq"] = 0x2288, ["\\supseteq"] = 0x2289,
        ["\\leq"] = 0x2270, ["\\le"] = 0x2270, ["\\geq"] = 0x2271, ["\\ge"] = 0x2271, ["\\sim"] = 0x2241,
        ["\\simeq"] = 0x2244, ["\\approx"] = 0x2249, ["\\cong"] = 0x2247, ["\\parallel"] = 0x2226,
        ["\\mid"] = 0x2224, ["\\exists"] = 0x2204, ["\\ni"] = 0x220C, ["\\prec"] = 0x2280, ["\\succ"] = 0x2281,
        ["\\vdash"] = 0x22AC, ["\\models"] = 0x22AD, ["\\asymp"] = 0x226D, ["\\leftarrow"] = 0x219A,
        ["\\rightarrow"] = 0x219B, ["\\Leftarrow"] = 0x21CD, ["\\Rightarrow"] = 0x21CF
    };

    public static bool TryGet(string name, out Symbol symbol) => Commands.TryGetValue(name, out symbol);

    public static AtomType TypeOf(int codePoint) => CharTypes.GetValueOrDefault(codePoint, AtomType.Ord);

    public static IEnumerable<string> Names => Commands.Keys;

    private static Dictionary<string, Symbol> Build()
    {
        var map = new Dictionary<string, Symbol>(StringComparer.Ordinal);
        var type = AtomType.Ord;
        var limits = false;
        string? name = null;
        foreach (var token in Table.Split((char[])[' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.EndsWith(':'))
            {
                var label = token[..^1];
                limits = label == "op+";
                type = label switch
                {
                    "bin" => AtomType.Bin,
                    "rel" => AtomType.Rel,
                    "op" or "op+" => AtomType.Op,
                    "open" => AtomType.Open,
                    "close" => AtomType.Close,
                    "inner" => AtomType.Inner,
                    "punct" => AtomType.Punct,
                    _ => AtomType.Ord
                };
                continue;
            }

            if (name is null)
            {
                name = token;
                continue;
            }

            map[name] = new Symbol(Convert.ToInt32(token, 16), type, limits);
            name = null;
        }

        return map;
    }

    private static Dictionary<int, AtomType> BuildCharTypes()
    {
        var map = new Dictionary<int, AtomType>();
        foreach (var symbol in Commands.Values)
        {
            map.TryAdd(symbol.CodePoint, symbol.Type);
        }

        foreach (var c in "+-*")
        {
            map[c] = AtomType.Bin;
        }

        foreach (var c in "=<>:")
        {
            map[c] = AtomType.Rel;
        }

        foreach (var c in ",;")
        {
            map[c] = AtomType.Punct;
        }

        foreach (var c in "([")
        {
            map[c] = AtomType.Open;
        }

        foreach (var c in ")]?!")
        {
            map[c] = AtomType.Close;
        }

        map['|'] = AtomType.Ord;
        map['/'] = AtomType.Ord;
        map['.'] = AtomType.Ord;
        map[0x2212] = AtomType.Bin;
        map[0x2026] = AtomType.Inner;
        return map;
    }
}

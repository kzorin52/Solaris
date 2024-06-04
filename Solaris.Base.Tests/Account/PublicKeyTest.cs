using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;

namespace Solaris.Base.Tests.Account;

[TestClass]
[TestSubject(typeof(PublicKey))]
public class PublicKeyTest
{
    #region PDA cases

    private static readonly (string acc, string pda)[] PDACases =
    [
        ("ENWx8CtawoSqRzuVUat7pthb5wJHJDWsUp43Ufkp8AAg", "GrbWJ5H28LzpfG7XL3AvAkhnJGEw8rZG5qQ3GvGfzeHA"),
        ("4VWYtUbswxBvAHiEViTLmiiv1QhJPuQdYgg9sCiVexvJ", "FFyBLXuTu8CiX3TCqHA8fX2CWXqezibnxU6KosXQijJv"),
        ("9Vt7AjmT2yMrQ8HERquGuhVsyEfou3T5kbiCKjCvp6mA", "5tf1FmZqyKtUH8aneZUGNWs1JZecafr9xAxwgC9E1S7L"),
        ("7WhN3Supv9tbM8CjtecpgyPMCYiDLmm25bV4L3PCQ4n2", "5zoT8gayeARnB6oKhs6sYsiUrViP6JsbTb5Yq3UF96vV"),
        ("J3v4wETEsrLn64UGB41Srk4uoUofzhLYUz21ahBxVjNv", "3EKSMc4BoCCe5FnPos3bNK4G38GUG5szrBUgzsG33q7Q"),
        ("FqXuFaRntJzf9maPb454ufgK5tJE8vCfvkAcpBrPztCF", "A77BsGufDKBt2nQhXafsT7K5QAtsCAsfjF6fz7xxXwmJ"),
        ("9EZdV6RBjVhtE6GL9uBAU44aFETv8dHkPMBdqHnJZEZn", "35fr9LCRtjnhsKy1TrHK72YRs4tftwfY96ev3AP6cnX6"),
        ("G95RGwHJPewoeWywpoWK3bcxWHLQCt6DWEmdY9fLKeGh", "J5QdbSNn5FekPe4BGdeoMMbcZuVZZNPJ9zerggAFEeN3"),
        ("9GYLHewXegNPSb6DfKAnha5bZgUMAJ5gjtXYgMA2w7sL", "72UNWbiYpXq9J7g5H4dbZePrNxpUHuVFh6gRCEMA1qYb"),
        ("83j5s8RDfG8WxWyhNQ2u7TwogfKA7bgMj1CTZULLjFyW", "8u2eSuWs5PGEAPBqWAhHygpUnWcf5pCtJGw3b3Rnvpp"),
        ("8hKijdMkbNyyRdpLcPWdyismpstyMY8rVyLTsb1Byodf", "3fajkStUtUNdg2vYe2t3Lfc8j2WfGhHt3Dv5AQTowaD8"),
        ("Da6J2e26LdixqdqqiRiQZkF3fj7t8MnuDnsJAvcut7WA", "auumE4nzFpqAyCW4QsysFkK7ZWicboKWCQbrxV7cpBG"),
        ("4KhYtQqpoZwCGmJYzuTegfM8hCsZJoGwoMSaJ5PxK6Yq", "5XZoXsDRQxmqGZfTBsExQXLUMWHtApGmEZQwteS6kn6f"),
        ("7nS2oKfTH2WLJZP1akVvpm4aQyoatCUev7SNw4sezXTm", "7bnh11XartVeHNjJ5qNtuMtWzBFUB4Q4EpaEKW3gXUxg"),
        ("BuBfBsysdQ1fymcq1SwfusVtTwVn2HQADK5FSWXtuSjL", "Abk3NEWMLAerbxodvhZS26bye12riNkJohSMNUTgNLxx"),
        ("7x5erEX9KaL3g8zkApSMr5zA43Nfk4d4NPNiRViSNSpH", "FPjZ7V2dNbf14afTusE78hQ5RsViVDV8peJYN6fXmZXh"),
        ("7vDtsQGa6knXmfWzwNiWUeD9dxUpndeWGUfrpCfZoZwr", "CLKscgF5Gu1B4Vmj2vshUVhd3W72ibRzwpDw5VL9adEG"),
        ("3qhGyX9eumBSjyDB6tCFLcKVQs9uKoqhH6zrrr3MZTab", "4uNZd12ES6fRrKrxFgvaSG43CWy2HT7ZLjBXaKu5pMVB"),
        ("FWNWSiX6jdC7c5UDVk5msyRMEZV9y3JLULw9ajskiSC8", "3zsTdj9J6srSE4ma2LxpW8xKnWC66aafYcFtLc4PstJN"),
        ("J9xdf4sLYmC8Drs4MZPgYCown8RoBzaW8i9TqeMmEEXy", "FPLndoJ2QyKEHyjZXFhta1bgv76gqVRdnbx8fKmbcXdj"),
        ("DCzuyBrLiVrMmLk6L1jeKJDgas44Rp8pk7Xqcro5h5Ne", "7DPkyFhqqR5mtAFP5wnPXKgc2Gzs6wcGcWzYoBvmSimd"),
        ("8Vbh8SamLxdAqFvguJhRqpPAtQBZoyQZRfVTQNPL3AgC", "jpAPgmSdgxML2S679h2ncAHzRmbAdZ2ytpBeJgH7fB2"),
        ("7cHnFD7WoMyqpUGxtEgCr21ypadzJAmQsCsZ9aZiaWwY", "A5ZayZUn2dA9AqBYNKhUsGK1Lz5f42qgQ5dKWovq5JnD"),
        ("8mgusSzr2BgtrL1MMFGAKtQ77QHDmTL2r7fRZd9xFUzK", "G85qES7gvT2RKAc8PWQsWgpoZcQLe1jwxwjpzRZSjCfU"),
        ("4TGmUq2caLPXKc2kir4FryuTRmedzt4XgmAHaZjpNHti", "3z6U4Vc638GZn3nspUN4hL4g5sJnUkqkVtWcu6fDeCAN"),
        ("GZdVvL95xeNxc5CPvAmqy2fQR2z8jkDmySvci5FvNTGq", "GmsXEene1XvszD4TByLv1p3GNMf7qcsWHLy7NgRuHKvP"),
        ("7t8FQtZYsJc3ExFzpdJr3CfSDcWnA6r5QrGsrwi4T5cA", "Hx2bMgoXSSacEXPd4G2QgWXNGYx9n7kJLtAmLen6ghZa"),
        ("7yTB1Qw2kijd9Wr6pE3TYCbb6sNFQf8GUtDdG6hKufrq", "4iCkCSmoaPKGGCsFdDGY8z4PDLFRbQLzzQWvMcHcufwJ"),
        ("F4eDN8FjZ4aZfjMr8igMtxeZ5XcMMc6di1sQLD4JfzPR", "Ch7dhrUGQxs4tYL3XMaeFuJvUJMq8BAH4yYJGPoSN24n"),
        ("B8QFesRyC9JtsDTSd2LtwApXUbaL23ttqyDdN8m2Bsdv", "6Qxmvomon5HuqpgyzeZZzVttiZFc9zfuTP9JgJwGoAFm"),
        ("3Gi8AzKgkBaRpPrX2CQp79yGNWWtD5TZsmbwLSBhoPPE", "79xWHNyKKTeScMJqG1HKV2hspbnjuH7iP4h5WiewY1TZ"),
        ("CajEqE4bPLGbXg1GKi5R2PrdL6z8d81jwxWS6hzRBpz9", "AMvJhfS8T199kjit3AquDDbZ22z224gedDBPzYEGRKx1"),
        ("7ZjU8ToJaNTCHgSfPGu8Ba82bbd1DvxJbWxuPUUCvQy9", "AkjvvogptQXZV4UTkgGAYU2QKN12B5DsCPK4NLXzzM8n"),
        ("D5VHheXKYALHZXCY5fXBdH8aLUjEfdZgyfG41UjBosuq", "9YZzggYWjF4BCBwKHAZ8rXs3oV9siRkK4EYZuxT92PCg"),
        ("FbZ95F8kyNgbi4CyZjFApdiHE7iVvXC2ssbE9Zk945Ym", "8KBo1FveHZU7CUWKnH1zoyyymYetgct12YXF76jZFqjC"),
        ("X5hGuTnSV63WVN7zQgCCAMKkAbdRMAhSKMpSKSnXYn8", "4jDUhawxkWf1aUjMdBVwFPVfCMbnEx8B1Dxu6HRX4TWd"),
        ("7dXN92ds2XraZjxqnMWZ4HuAaJfZQFaBKWjkuJdkcZYe", "96ekNgFGEz4tbkfCmVXteG8cdPqMdKJso9NcNRC3881q"),
        ("5fLvX6WfAuNU5hJbEFV1agtGUZZWMxiPQBvpx4mSUuJN", "41ZBdJMitZNW9g7VMn1cDrso2zbRVGurp8WrXBsAnHHH"),
        ("2V3zxRmoEUb6nsZkDeyycjHUKtjvp1MAs58FXXXBfasK", "7ui625Dxc3rfYdJ13jYBsbRGd5zLt9WchMfbFUsS8XZu"),
        ("F62d3VqaQgzuSLHnUNM6NxfczPWGAM2b8ATpwSnH5Vze", "4SZbPdgztZjDPGrYdQL9BQMYqTuyjNWMwFhnQGmrwZYJ"),
        ("CxDdT1KkdDP92rQE22TNFMHevRZTA1WzhNndsHBfyiU", "H9Vz79Kke9N79rA9WH3K1HQtLPcxWZpk259bNu5qto4p"),
        ("AWXhotW1d4WZNwJCWzpmQ48KXbmDLm2bQukh85RSKd4q", "quNdxRjZ664MgyNoZQjJ1Wa8yUfsGpWLezRGxrHAUfL"),
        ("44FZGzFNYzXVpKmmEV7B1RXt8fuvqqV8NJJRHQ3zyitQ", "4FFDrFar5FEFcUHQNvL82zu8EMjFAheizdGJRRCbAAk6"),
        ("4bs8zn8eXzVRvCU76628rJQWUNjwzmwBJr2K5ankLdBi", "2kw5i1Wp1NVGkmnmJ3of7deC3MDQcyVD6iGuxPaejDX5"),
        ("76BMNkCJATNEhZhFZUGnshwHT7E7Z6vJHjsdvAQhdw2N", "ZSvo8vArmUu9rwgShsJSoyFxrvfQqQCjKLBYmhUou9g"),
        ("3ZQpNSSbBJAvjAvn86UvFGh9SvpnaCoEjaA69wgjX3Fp", "DCQEvrUruHrXBYGXoArrvkQzVVS5e6HtHZF9WppeZMY7"),
        ("HjFrjbLBV3FLzx1iNXAiFU2ySFczJgmgxHPSQu5Q7buQ", "7GKiJkz4MqCj81aRJWRm15HCK7BxaL9CpjytPprvGoEG"),
        ("XmuM4V8ffNnaxRRVCHgd2MRQ4P8hNafaiMcDxTuJAd6", "3cZ9cNFZBCj84svrmNh2AVphGdYCCTzZiuGDrkgYiZu7"),
        ("DYZerqCtgaTHMt5DzGConToNWsomHmYmA8QofooUuq2W", "HKcJ3HeJ5cdfxyDKkXDqSdRC5yS2GYyBbdxJSXTCV8NQ"),
        ("EgV8gv9VCH2Li3t8yx2hEaAjLLcanXNCRyiZrYsa1WmJ", "DWmtu8YC25ymiPBzzPyshErf5FQe9SGYHZ9uzsaSX8PG")
    ];

    #endregion

    [TestMethod]
    public void PDATest() // covering encoding; PDA; basic ctor functions
    {
        foreach (var (acc, pda) in PDACases)
        {
            var der = GetExtensions(acc).Key;
            Assert.AreEqual(pda, der);
        }
    }

    private static readonly PublicKey CandyMachineProgram = "ArAA6CZC123yMJLUe4uisBEgvfuw2WEvex9iFmFCYiXv";
    public static PublicKey GetExtensions(PublicKey candyMachine)
    {
        return PublicKey.FindProgramAddress(CandyMachineProgram, "extension"u8.ToArray(), candyMachine).Key;
    }

    #region OnCurve Cases

    private static readonly (string pub, bool onCurve)[] OnCurveCases =
    [
        ("BBK7u9zPqUdxdX4jUxYoJ3XfyT34gbEV45xZCBN6dv6j", true),
        ("8psSuPgPa8c1mb85DbTyoVtvNaG2ZVrQkGLb8Z1QiYQc", false),
        ("EAakxrpHP69yQaKPMJwEkhYTto1sJDdUpnfSLEAwtRh7", false),
        ("741BtbbKKf3sf79PCrwiwA8BLNzN11A41qoN3Vf2S9qr", false),
        ("BieNKWADFhTioXoF5177Yh1CyhdvgSRu5QR7fhyU3E6U", true),
        ("G5hPU5bA3NphTx5C9DZ5K4XZdrjxpSiZLMCvLk68EYzA", false),
        ("EpuRq2Gb1HLNyWb32G7ik9LPNgrLtXagi7QCrfrASfgo", true),
        ("UTVLUqvwVVoKCcpeFECgxzuCzLXco5TPJGgJbACWiPL", true),
        ("FZe8Q9jbNym8WJb9mZXVUBVuweLZotQBsJyhuUXvFoJm", false),
        ("41incBht3Z4KCMLDTWFQmSNxuYroJmKximjphvXRoPK7", true),
        ("78iFVorL1nLakY5Z4XBuMYXnLojrUf2hy8iyBM3zD6GT", true),
        ("5KJPKHCPexKTT7N3sLg2iCbBPefHodPiMiiucsDTkcSA", false),
        ("5hYtuGBy6HunCnaEz1EEgfRZJ73oxQY3qh7P99q64Uzt", false),
        ("CJ1hNwhTi4Q9YR7yYGa239YoRTjv3MWe2av8ZWx2RBv6", true),
        ("5QZDbkwkHqFGQkHpMpnjLRPttDXUYvC2gd4J2xpr8D3g", false),
        ("HnwmqV7UPs5Gae4UsrKuYwVQFxvjMqANTTE5QPrtkdBE", true),
        ("AtyTeKtCSnugiE1pLb1kdDEWbUPPUYdzD3f6mkw85mB1", true),
        ("25JaYKd5EGWqFqV9Xg7tNGe5qUC3Z5q8ENWop6eZUsz5", false),
        ("3MRmyY4NHmrtV49iwX989CivKv7uZeLTvL9DDH3wo4f7", true),
        ("3RUhzanVqJNc8gv4J5L1JYD4eh9fnLGLVRx1c2QRiFAM", true),
        ("ATeL4xod3r8XAZQMkGRsVsPAqT23xZxmjEjN66ycv8HL", true),
        ("9gMPZ2o9BXLq3zsWK2tj8FtAQrDTcF9dG8XgG9GM3BxJ", true),
        ("FTcGQsaT7arqqpt5Qo1SbD676jRRwS1Zn1EkW7SXX1Nk", false),
        ("38RmPLLgFY9UwjZPmLF4qTr4caY6tZ83PrF148S323pw", false),
        ("CvqHwLf8QfmQvBq487Ypx9ZWPjYq16oMZVCQNkeoS7i3", false),
        ("HLtvzyhhtdaZPv2t2sgHqDPV6rSbkXPZgqzMxFLc2bPc", true),
        ("6uVbJr65m35DVbsDPnpoWEmQMiEKQB7wXZBwwrGk2EP", false),
        ("5YmPcJpCSTgsS5168ykrpvvnfFaNPj1mKYsRaUdtbv9r", true),
        ("EpknLT9hMhy8M2hs4GhxJCXi7DCv3FXrVaV88YDHWeUF", true),
        ("2TC96fnqZY4AS4Mv1dk6QGyAEmTkBuWH3fo6dsFF2ocr", true),
        ("G7SPwEyo9VEm3ogWXCfvo9jx9YP5kEfT2AGZRB5uak6h", true),
        ("5VKPXThTymdrnxpRLdPPjnmPG6Kz5by3f82APHzDvPxU", false),
        ("BhPndF8gLtUotX9z9v9Nwoxf2ggFN7Qf1qL9DBk7Xxhq", false),
        ("5RkowCKqSzvGfwpE3uVt8v5o9qmiCPZztMkvJgAbKgn2", false),
        ("4Ua71TSijz85kdRU642nWawUF3kVAPECoXYhaPaK3T8L", true),
        ("5qaGG9wF6MZPaajGraELtwVnWLSDamgubup4VCvdp5e3", false),
        ("DC9pGRbvD6GEWe5J3QBiQ6RT6RM9brq5o8gNfuBUmdoX", false),
        ("AJZWKUMS2ySCUFVY6iBWM14zfYpnqWF2HELJkfKdbiFh", false),
        ("BLJPHYNw4xjNfABb9zTibg2ytVgEL1Ey93B1URq8qZ3A", true),
        ("72zLD2oqca9NvbUXseGS3bH3TWks8BvVaBmyUeCwytrk", true),
        ("BKddA91W49uzgMhyCtDxtEmCVuFdkpxNisqGpGB7LrNz", true),
        ("4zehiGZjgWwffpdm1kG1bwS9wkfFd2ZuU3Q4RPeTxRe9", false),
        ("BDBd5yY4j1QW4pDgBtE4pVtZkFfi6pcjJpmzRmQ7QptB", true),
        ("6bn1JScH4PujYN1s3rQoAwQa8KSsP23aR45AjLM6gE8W", true),
        ("HbDbp8mjPJTLLW1Z2C99W3CPTjaJX1m4JiRyxxg7QLm7", false),
        ("HbFLD5FNqxMQNo2YVFsuwDH4Ro4BbVFi1qBciu6viVn6", true),
        ("2HNS6YHHWmjgYFqw6Kud7zZ7FuwY9swzgU4GaUUGn8A1", true),
        ("EohDYmkJSRp7jHKxhGjUoiyVGYW3jVP5ppSRT13Lh5Gc", true),
        ("ApX7AaCPziVdDBye65SCcXB6PYDi85WGTXUebzGM9QkN", false),
        ("4aP7z4ajiLcX7ASox27NbBLiatXBbSgTJxkzhw7Fp8Qa", false)
    ];

    #endregion

    [TestMethod]
    public void OnCurveTests() // covering Ed25519Extensions
    {
        foreach (var (pub, onCurve) in OnCurveCases)
        {
            Assert.AreEqual(onCurve, new PublicKey(pub).IsOnCurve());
        }
    }
}
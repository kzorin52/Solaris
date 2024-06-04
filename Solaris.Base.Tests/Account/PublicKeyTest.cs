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
            Assert.AreEqual(pda, der, $"Expected: {pda}; Actual: {der}");
        }
    }

    private static readonly PublicKey CandyMachineProgram = "ArAA6CZC123yMJLUe4uisBEgvfuw2WEvex9iFmFCYiXv";
    public static PublicKey GetExtensions(PublicKey candyMachine)
    {
        return PublicKey.FindProgramAddress(CandyMachineProgram, "extension"u8.ToArray(), candyMachine).Key;
    }
}
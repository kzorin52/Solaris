using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;
using Solaris.Base.Crypto;

namespace Solaris.Base.Tests.Account;

[TestClass]
[TestSubject(typeof(PrivateKey))]
public class PrivateKeyTest
{
    #region Validation Cases

    private static readonly string[] ValidationCases =
    [
        "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"
    ];

    #endregion

    [TestMethod]
    public void ValidateTest() // covering keypair derivation; encoding
    {
        foreach (var @case in ValidationCases)
        {
            var privateKey = new PrivateKey(@case);
            Assert.IsTrue(privateKey.Validate());
        }
    }

    #region Signature Cases

    private static readonly PrivateKey SignatureAccount =
        "5zVUbi16FLVK8Tsji1HE2ZH3y4Ypjgf9WSipR15aphDenCh36BRibYDMXe9ZFzqXds2GZLkiQkY9VnZ5f4Kzcqts";

    private static readonly (string message, string sig)[] SignatureCases =
    [
        ("BECgu2DVnPiNDm", "5iE5WYxrMKY8e8vJauGGM5bra5CH9Dvg7FC284JMCG5FqCh2Ki6N9dRV2fvdyeCv1qP3mRsimFEz6KoQ3rQBys17"),
        ("DT6BVn5jZ35C9V", "59KT1hCtyGB1QJvtNu3VLmZGFT3vgk3QYmPDK4AMroaEMLrZwrc1YBm71F68hx7ZoXxwDaMNgbjZt7jmGXqTxpF8"),
        ("4k6KAwW3BFBJjs", "TEe5QE73Wu2fNS1Pw7SYBK1hdhKUWKjG5hwZskCtw6n3L3ECYLaBwGEQnBubuutxxG1VpsqJL6bwa6ffvanUsvN"),
        ("3bKiNwJANSqV6G", "3hwm5vcgnQxzR6ZM9EuPVZJMqGBWQnXDXp1hR5DcSc5Lg5y7VayXGjCi38okg4ZnUYaZzA2xthcYSaSE6w9gyjkp"),
        ("C45fdQfJMb3AYT", "65G9jVPzuMUsJp5DjpQXNUXbZHqAs96XRfdmLu98tKVmsTex5CeqSTSX2wdvKJ9UQ5wGLGGw9foGQiycDg1VqaQo"),
        ("CUY6mGDNdtLoj9", "3EsS2gMywamZUR57pn5gnprHmrDdwxEwqJKGFN57RRA9g2H2g8HfHx4CHUhTa6Z9aZWi6J9eNGy8apDK9jkZnx9F"),
        ("3kEdtdXkoWwC3v", "2VMSaYjchnS19sgPJwFDHSVRP1tqKvqnt7kykgqEXXLPduvFo3EoyhLfQs5p95dSzEcBy5y2fZHaTvzRFLnuZRsw"),
        ("4RTCvivf78g3wq", "5unN4Pv6hrRPzcoYUoe7jpgYXcPyXNnqZ4uFsZmcqtfAS6roxaAkQdBhfAsW7WCjwCiu4N8ZYkAGKn9q2SgJMfV2"),
        ("5fSDQ2GzAXvnTm", "5kAfRzSe2kwwoGCGh4mXAYr1Db4GQG4EWGNnfEZGZSAJD6FTqboBavKaMQW4obu7W3V5yhh2fZxGRzSFuhXuWeUg"),
        ("5fjUK7VbxZHFx8", "5FFSBA6mdnfjzaSpjSvdwi6MqewYDxnkgz4Mva7UhbXmTdKfzLFQwmUimnwmCNqbpNoD6Ra1TetmU4T18fUd2V8u"),
        ("3uSwyWJyGH2oEG", "2KEgSjPcnEwcaES5ipn9BSW82hSc8GpPQuZjUSwECbDJjKMet3YKuuDWJjRJcywmq5Zs11bhaL7smx33bYqWmA6t"),
        ("BJtQSrKeGxVLTa", "2MDwQF6VZzeZidNbrGezUMVYKZQT7ufBhPev4KoRD2vbjYjXYGhKRdokmKk7Pc1HgjkPCU3kj54vSjiofT73h6b5"),
        ("8UfX4HrXv1S9eG", "282XLx4UFzdXGcXfKSaGKPekYiBZRh4FXqymG1yzXXJ3Cqpsuoaff2FJ7xp8GKAkYKUqNNSYHAr819rM2oSdakTD"),
        ("77K2KKeGTceVhM", "34mWfbKQY7nnGzeQKB5d4FdDpWd8LGUzc4iny881r8yd3CeigUhvjvwhT7HLQpZqj8EXdztaQU1Udx5MxkNmsLeF"),
        ("5TvbFKZXhc1Kn6", "5LpTFsj2f9YuM3etGbxreugMLHrUrVxXwhU7QYrgXgkFMQ2wZ6JV21Dze5oDWkCKroCg9eJnLoQoGqU7Hz5xkszv"),
        ("7V19Bw5XRakDNL", "kqu4t1TNVH3Sw46yAkvcgKm2NLUjWQKZMhQDpSPrYqExaBVnd2AHBWhoAb5xcAKAEu2Shpq1KRdCq2a1WeKsu7B"),
        ("7531pXcx2wiTsX", "4jmG34w2JHZtpFo27s5k8YtAXL7xoUqhd3LMhFA82H3XUqEXUEs2jqzB44En5LnMFohQaToNAC3mZCGbzAwLdgAk"),
        ("918Xc2bZjED8At", "4yBsbcoLZJpwrEwDA8zDWPrAwTE4n8oVmQajrhhh6ZP3EgaeinwE6udxgW1ZU7RZLJKTBL1QTvJWidxvnUDu4GsR"),
        ("9tGCFZGD8FfZDn", "5VtkhFTMfuoGhStFKLetJPBYCppg3gHbDvknHLg6YdJvkjFFHAUwPNjC4osv5Dd6jXbt8AFkq25wpWtuiBAL4xJj"),
        ("ExUSWsDD9fs3DR", "4f6id6sBur3zXXJr3qY5hKCDGV43f9CMz3YEGbqzZzbiPwVVHSG5BXQudJJGGiuCM3SoELVeaaU6yBP8xwRMPPhq"),
        ("97sB6tvZWpFHqJ", "2nVEvv2biRFcAnPwPaJ6TuBxU9oqt4znY2q3szHj7pNa6jD97HcnVLyNsGSgUwRyuLPzVVQsK8iod9yi826dTQRp"),
        ("b3zjgsks8bGN4", "55CFK2zhUtLA3v2yzkSNDeZjX6HY4rJwTk9L8qcLQevjzVJCP8MmoM6h5XGQUvUMn8iajFVWdHi2iALRttB4iqrJ"),
        ("7pK7M6wjxLaKiL", "SsQSe3Mtcinx6bL56iZZkW7154BkEZ8hpkYWT1L3HzuPZ2Pqgg6w7DDmFum8yExE1GozvVyKmmYxEWvqSEa9BXa"),
        ("9suSj7e9GnxbcT", "4qh1anvCrb4oxidYShLHEesxcYuyeEFqxd3msroUzuMHBBbB8Yyzs853BNB2HRnq2r6pUxNQmFiW1Titicucjhyr"),
        ("77UmHS4NfHFWez", "aNxtPkryGBsfEMwFr9SxD1ekdsMcBssCLUtRTnuVieeg8pN9qCT4NPoPjh4iQM1UCzPTYik9uWUQwnLWCoosz6k"),
        ("6HqqYrsJiFBYe1", "63J6q5M3qSMTM8gr5GUngibxFSyhy1igVTmYoXrYuWfKcZhHreFN4Js1Efu5C4wwiZ8N7Vg7tfLjbTRcAAyCuxHR"),
        ("6x9qiv8rMW96y8", "3PibTbdJA4RpacKoLmdmpDkjGyYWXg9QrpP9Q4w1EPMngJV3Xz6VDTqKQSdJM2s6miF11bmy6L8YCWZ55waEpZ29"),
        ("87fyTiikxEinnv", "5FL6N2D3TFbGhS5R1gmcAtEdS4B8qa7tLRxNEDKM2vRX41qZkmvPT5nrUED6BKmj3U9KUn5hejUHRVqGa58NGGZW"),
        ("Agg5FmjFKNvtVr", "4aBAS1m9vZSCH5SqFL5kytpgVkbwUzfM8vByiQQBpn4PwZk4XVdSvNzZWiwCvLYJMvdZCuUxGS7GTUKnWXYg6RA4"),
        ("3tUjnj8aqwENXe", "3K4YCwBSg5kSF5bYGHucHwTZLVohM8cX8tevR4cFygdHXzCRPeGzRtYZgzFNajyghW78zcwnp1moEsGR1MzP436X"),
        ("CXuTLFxYxQdau9", "4eHkTRFR8sdRL16HGniC3gLV8nte8R3jmY4QizwFZ3CrMiyj3qtqHoVmitgXKSYYi3r2kXUbtiH6v7W1mhxEGsRC"),
        ("3dxpqfETmzUeWF", "4ytnhepXkvdEyNCmVPixbHyPQbBpswPoq4H6U5Hd9z3cS4GEjQfzigA4Zk5amEHjU2S2DP6dErH4YbpdVkJq6yqb"),
        ("7eX2tosNNGcjCM", "3JbDp5nvUxrhpf1jbWHCTXipMyR74iTb16s9yp5BoyvwuvwXfGateuXS5htBazz2fs1eqTPhGv7REB45Uj3GBF2J"),
        ("F6L2i13YQ3hMo2", "Z5YND7MX1FQFZXDqK228zzcbkT5tTi79KmA4GsvizTA2q7h3yoHPByyX6E2Beji4zKvjcc7wxoPCfj4g9ceWZUt"),
        ("92EsohBPAxKKS2", "376yKSidcZMurTFD8CdEKKy63tE1xRMm9MPwZqCS5hgxSWEv9gutegW9Hm5ZHjihnJHJ5Qbehk4JqLXp8GRo14sc"),
        ("9BwT5Pcyh25EK8", "2BnBZL7mGHPrVS1MsGUPwoBGVJTYgQUvwdczoWEndesE5qACPdpKAowVyBc4vXJ5dXPg719tuaHkAo2mzzdQKzLv"),
        ("4ZqLatcqLBaCHL", "4zY4VDt7CMhNAkDxFv5scbcLuUXopgByhwkmyZdzeHuyzE8rHHY25cwPW5goymHKheYub6TzLwaTarc7bei1UCVA"),
        ("55UzZ9SREMUCMc", "5uFcuTi4RZm6kAW724RWPnUmWjAbzyp2S5Ax2QU9k1rzCG6oxQJHD4iEeRqXxzsuEFMf4YP2qTXuJ8JDERcVyX54"),
        ("TSzGixADdtGsJ", "3bvuAM9oMFhjFXSeescAw5JQjH5oKDEpEpN29vgkQXJwPDnJV54e6j21CdFHn1HJw5btZ9n4sGPRsm8G6tSm7JTx"),
        ("51R6Ej6ZJYMeNb", "2fa7skDS2m8YhgC2JrDfrnTDvkMGVgBZhxhSEvn5KSzJRSpWWGDcJ6qqbRNSGyW5GcJxPJ4yMMAVPrzquhfNPE8Q"),
        ("3KoM4kDfGdPyqF", "nNadkHPGZdmxiz27L5G1kSgLrddGuHKLnELpHb56vUqicx46sgQCQf2GMdpNWmzfBuxWLw9LyfYKk2Ta8y9qdHH"),
        ("i3csynTwbBy4z", "3nKEzRKh5TrP4Nj5C2RAnBf9NeQR5SuuDCCehyWdppVvhGq9TfYoKdV3CQTtT3Nq96Qg8ZX6LBjd21LUN9KuqS12"),
        ("96Bsyk1y3gLVrd", "usXGHBhNGdGC4pUoUgze9hczqzBo46cq2T5Rdx1NANsCT6G9Sbt4s5tAfzSXxrrXQFtQnp68PVsyoWDUjKJj6Br"),
        ("AL4VxBQuyYagxG", "23evmQjMJw9NGv17nUodSATuMLduLPH59TXFoZhapL1y68c48X2JYnHn34HM54GgHLCda5r6wbtuGKywLrHxjCD1"),
        ("CjuBU4yqrQ5piH", "5u8S4ybvk5TpVH472b7HtuUAHbmFEVEPiyNQLxHy2mreqjCi1XXeUctMh2PKvepCmZzjk6Vj3vZvWMFiswqbBFq8"),
        ("7b5TRYdHu6nHpC", "X8pbMrciCF1ipEythRXv6TTEyZmsDqryuJovfynjDzSjQzm7k9xo6TrY6rsi3YTwjm1kU4DuaTkbNg9r8ozsghP"),
        ("F4NoeuX4xfZTzC", "3jZNa2iTXGndJLoH7S2Y73ZTUyWHG4FfC3wmDBQDNoT2DdGQPgRJGdVQCNyvjCbrUj21trQoPqCeZtxZgu6iCP18"),
        ("3Vw2i5dKRP2HnY", "e4Ly691zvZVe3Gg73QCtJ4hdpdeiGWHb75jac2jSX3JrdS4Er1dnEABQYg9FL96jdHj1jQNXBPCXsc6hqkhu5E8"),
        ("ErAxPKP5rTG4qE", "TfxtBbPYNfnrJEP4MTj5id7KuqVBvw3pMRcdty9tM6qLMpXEScM1u9GowVXxELtEUZeu4ewPYx6k4WfoyxkgbAM"),
        ("ztSDydW4tGpZ9", "5sZJ3HmQ1SpQHFPkmdLxaC84UZECpjw9wuh8dkPS6YB1CiL7pr3GwUt3XY4kfqu7qsMZRd7jT6pyopf5qgNBR2xi")
    ];

    #endregion

    [TestMethod]
    public void SignatureTest() // covering PrivateKey.Sign and PublicKey.Verify methods
    {
        foreach (var (message, sig) in SignatureCases)
        {
            var msgBytes = Base58.DecodeData(message);

            var actualSigBytes = SignatureAccount.Sign(msgBytes);
            var actualSig = Base58.EncodeData(actualSigBytes);

            Assert.AreEqual(sig, actualSig);
            Assert.IsTrue(SignatureAccount.PublicKey.Verify(msgBytes, actualSigBytes));
        }
    }
}
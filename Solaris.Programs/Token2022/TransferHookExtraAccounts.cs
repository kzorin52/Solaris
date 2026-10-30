using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static class TransferHookExtraAccounts
{
    public static PublicKey DeriveValidationAccount(PublicKey mint, PublicKey transferHookProgram, out byte bump)
    {
        var result = PublicKey.FindProgramAddress(transferHookProgram, "extra-account-metas"u8, mint.KeySpan);
        bump = result.Bump;
        return result.Key ?? throw new InvalidOperationException("Unable to derive transfer hook validation account.");
    }

    public static AccountMeta[] Resolve(PublicKey transferHookProgram, PublicKey source, PublicKey mint,
        PublicKey destination, PublicKey owner, ulong amount, TransferHookExtraAccountMetaList validation,
        Func<PublicKey, ReadOnlyMemory<byte>>? getAccountData = null)
    {
        var accounts = new AccountMeta[validation.Accounts.Length + 2];
        Resolve(transferHookProgram, source, mint, destination, owner, amount, validation, accounts, getAccountData);
        return accounts;
    }

    public static int Resolve(PublicKey transferHookProgram, PublicKey source, PublicKey mint,
        PublicKey destination, PublicKey owner, ulong amount, TransferHookExtraAccountMetaList validation,
        Span<AccountMeta> output, Func<PublicKey, ReadOnlyMemory<byte>>? getAccountData = null)
    {
        var count = validation.Accounts.Length;
        if (output.Length < count + 2) throw new ArgumentException("Transfer hook account buffer is too small.", nameof(output));
        var validationAddress = validation.PublicKey ?? DeriveValidationAccount(mint, transferHookProgram, out _);
        Span<byte> executeData = stackalloc byte[16];
        var serializer = new BorshSerializer(executeData);
        serializer.Write(TransferHookExtraAccountMetaList.ExecuteDiscriminator);
        serializer.WriteInteger(amount);
        var context = new ResolutionContext(transferHookProgram, source, mint, destination, owner, validationAddress, executeData, getAccountData);
        for (var i = 0; i < count; i++)
        {
            var meta = validation.Accounts[i];
            var previous = output[..i];
            var address = context.ResolveAddress(meta, previous);
            var signer = meta.IsSigner;
            var writable = meta.IsWritable;
            var found = false;
            var previousSigner = false;
            var previousWritable = false;
            for (var j = 0; j < i + 5; j++)
            {
                if (context.Previous(j, previous) != address) continue;
                found = true;
                if (j < 5) continue;
                previousSigner |= previous[j - 5].IsSigner;
                previousWritable |= previous[j - 5].IsWritable;
            }
            if (found)
            {
                signer &= previousSigner;
                writable &= previousWritable;
            }
            output[i] = new AccountMeta { Account = address, IsSigner = signer, IsWritable = writable };
        }
        output[count] = AccountMeta.ReadOnly(transferHookProgram);
        output[count + 1] = AccountMeta.ReadOnly(validationAddress);
        return count + 2;
    }

    private readonly ref struct ResolutionContext(PublicKey program, PublicKey source, PublicKey mint,
        PublicKey destination, PublicKey owner, PublicKey validation, ReadOnlySpan<byte> instructionData,
        Func<PublicKey, ReadOnlyMemory<byte>>? getAccountData)
    {
        private readonly ReadOnlySpan<byte> _instructionData = instructionData;

        public PublicKey Previous(int index, ReadOnlySpan<AccountMeta> resolved) => index switch
        {
            0 => source,
            1 => mint,
            2 => destination,
            3 => owner,
            4 => validation,
            _ when (uint)(index - 5) < (uint)resolved.Length => resolved[index - 5].Account,
            _ => throw new ArgumentException("Transfer hook configuration references an unresolved account.")
        };

        private ReadOnlyMemory<byte> AccountData(int index, ReadOnlySpan<AccountMeta> resolved)
        {
            if (getAccountData is null) throw new InvalidOperationException("Transfer hook account-data resolution requires an account data provider.");
            return getAccountData(Previous(index, resolved));
        }

        public PublicKey ResolveAddress(TransferHookExtraAccountMeta meta, ReadOnlySpan<AccountMeta> resolved)
        {
            var config = meta.AddressConfig;
            var des = new BorshDeserializer(config.AsSpan());
            if (meta.Discriminator == 0) return new PublicKey(in config);
            if (meta.Discriminator == 2)
            {
                var kind = des.Byte();
                if (kind == 1) return new PublicKey(_instructionData.Slice(des.Byte(), 32));
                if (kind == 2)
                {
                    var data = AccountData(des.Byte(), resolved);
                    return new PublicKey(data.Span.Slice(des.Byte(), 32));
                }
                throw new ArgumentException("Invalid transfer hook public-key data configuration.");
            }
            var derivationProgram = meta.Discriminator switch
            {
                1 => program,
                >= 128 => Previous(meta.Discriminator - 128, resolved),
                _ => throw new ArgumentException("Invalid transfer hook address discriminator.")
            };
            Span<byte> seeds = stackalloc byte[15 * 32];
            var offset = 0;
            var seedCount = 0;
            while (des.Remaining != 0)
            {
                var kind = des.Byte();
                if (kind == 0) break;
                if (++seedCount > 15) throw new ArgumentException("A transfer hook PDA cannot use more than 15 seeds.");
                switch (kind)
                {
                    case 1:
                        Append(des.Span(des.Byte()), seeds, ref offset);
                        break;
                    case 2:
                    {
                        var index = des.Byte();
                        Append(_instructionData.Slice(index, des.Byte()), seeds, ref offset);
                        break;
                    }
                    case 3:
                        Append(Previous(des.Byte(), resolved).KeySpan, seeds, ref offset);
                        break;
                    case 4:
                    {
                        var data = AccountData(des.Byte(), resolved);
                        var index = des.Byte();
                        Append(data.Span.Slice(index, des.Byte()), seeds, ref offset);
                        break;
                    }
                    default:
                        throw new ArgumentException("Invalid transfer hook PDA seed discriminator.");
                }
            }
            var result = PublicKey.FindProgramAddress(derivationProgram, seeds[..offset]);
            return result.Key ?? throw new InvalidOperationException("Unable to derive transfer hook extra account.");
        }

        private static void Append(ReadOnlySpan<byte> seed, Span<byte> output, ref int offset)
        {
            if (seed.Length > 32) throw new ArgumentException("Transfer hook PDA seeds cannot exceed 32 bytes.");
            seed.CopyTo(output[offset..]);
            offset += seed.Length;
        }
    }
}

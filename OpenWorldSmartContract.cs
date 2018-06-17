using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class OPWToken : Framework.SmartContract
    {
        //Token Settings
        public static string Name() => "Open World";
        public static string Symbol() => "OPW";

        //超级管理员账户
        public static readonly byte[] OWNER = "AKzwJJ9fHfY4WQ8nKWCLRk6MscFiaBoZ6M".ToScriptHash();

        //因子
        public static byte Decimals() => 8;
        private const ulong FACTOR = 100000000;

        //NEO Asset

        private static readonly byte[] NEO_ASSET_ID = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

        //总计数量
        private const ulong TOKEN_MAX_SUPPLY = 1000000000 * FACTOR;
        private const ulong TOTAL_AMOUNT = TOKEN_MAX_SUPPLY;

        // Storage key for the current total supply
        public const String TOKEN_TOTAL_SUPPLY_KEY = "totalSupply";

        public static readonly string PREFIX_APPROVE = "a";
        public static readonly string PREFIX_BALANCE = "b";
        public static readonly string PREFIX_LOCK = "l";

        public static readonly string PRIVATE_WHITELISTED_KEY = "pw";
        public static readonly string WHITELISTED_KEY = "w";
        public static readonly string BALANCE_KEY = "b";

        [DisplayName("whitelistRegister")]
        public static event Action<byte[]> OnWhitelistRegister;
        
        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> Refund;

        [DisplayName("approve")]
        public static event Action<byte[], byte[], BigInteger> Approved;

        [DisplayName("locked")]
        public static event Action<byte[], byte[], BigInteger, BigInteger> Locked;

        [DisplayName("unlocked")]
        public static event Action<byte[], BigInteger, BigInteger> Unlocked;
     
        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (OWNER.Length == 20)
                {
                    return Runtime.CheckWitness(OWNER);
                }
                else if (OWNER.Length == 33)
                {
                    // if param Owner is public key
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, OWNER);
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "deploy") return Deploy();
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (operation == "decimals") return Decimals();
                if (operation == "approve")
                {
                    if (args.Length != 3) return false;
                    byte[] owner = (byte[])args[0];
                    byte[] spender = (byte[])args[1];
                    BigInteger amount = (BigInteger)args[2];
                    return Approve(owner, spender, amount);
                }
                if (operation == "allowance")
                {
                    if (args.Length != 2) return false;
                    byte[] owner = (byte[])args[0];
                    byte[] spender = (byte[])args[1];
                    return Allowance(owner, spender);
                }
                if (operation == "transferFrom")
                {
                    if (args.Length != 4) return false;
                    byte[] originator = (byte[])args[0];
                    byte[] from = (byte[])args[1];
                    byte[] to = (byte[])args[2];
                    BigInteger amount = (BigInteger)args[3];
                    return TransferFrom(originator, from, to, amount);
                }

                if (operation == "whitelistRegister")
                {
                    return WhitelistRegister(args);
                }

                if (operation == "isWhitelisted")
                {

                    if (args.Length == 1)
                    {
                        return IsWhitelisted((byte[])args[0]);
                    }
                }

                if (operation == "airdropTokens")
                {

                    if (args.Length == 2)
                    {
                        return AirdropTokens((byte[])args[0], (BigInteger)args[1]);
                    }
                }

                if (operation == "lock")
                {
                    if (args.Length != 4) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger amount = (BigInteger)args[2];
                    BigInteger lockTime = (BigInteger)args[3];
                    return Lock(from, to, amount, lockTime);
                }
                if (operation == "unlock")
                {
                    if (args.Length != 2) return false;
                    byte[] to = (byte[])args[0];
                    BigInteger time = (BigInteger)args[1];
                    return Unlock(to, time);
                }
                if (operation == "lockedBalance")
                {
                    if (args.Length != 2) return false;
                    byte[] to = (byte[])args[0];
                    BigInteger time = (BigInteger)args[1];
                    return LockedBalance(to, time);
                }
            }

            byte[] sender = GetSender();
            ulong contribute_value = GetContributeValue();
            if (contribute_value > 0 && sender.Length != 0)
            {
                Refund(sender, contribute_value);
            }

            return false;
        }

        // parameters initialization
        public static bool Deploy()
        {
            if (!Helper.IsOwner())
            {
                return false;
            }

            BigInteger deployed = Storage.Get(Storage.CurrentContext, "deployed").AsBigInteger();
            if (deployed != 0) return false;

            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            Storage.Put(Storage.CurrentContext, TOKEN_TOTAL_SUPPLY_KEY, TOKEN_MAX_SUPPLY);
            Transferred(null, OWNER, TOKEN_MAX_SUPPLY);

            Storage.Put(Storage.CurrentContext, "deployed", 1);
            return true;
        }

        // Current total supplied tokens.
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext,TOKEN_TOTAL_SUPPLY_KEY).AsBigInteger();
        }

        public static bool AddToTotalSupply(BigInteger amount)
        {
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, TOKEN_TOTAL_SUPPLY_KEY).AsBigInteger();

            totalSupply = totalSupply + amount;

            Storage.Put(Storage.CurrentContext, TOKEN_TOTAL_SUPPLY_KEY, totalSupply);

            return true;
        }

        // Function is called when someone wants to transfer tokens.
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            if (from == to) return true;

            BigInteger fromValue = GetBalance(from);
            if (fromValue < value) return false;
            byte[] fromKey = GetStorageKey(PREFIX_BALANCE, from);
            WorkingWithStorage(fromKey, fromValue, value);

            BigInteger toValue = GetBalance(to);
            byte[] toKey = GetStorageKey(PREFIX_BALANCE, to);
            Storage.Put(Storage.CurrentContext, toKey, toValue + value);
            Transferred(from, to, value);
            return true;
        }

        // Transfer tokens on behalf of `from` to `to`, requires allowance
        public static bool TransferFrom(byte[] originator, byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(originator)) return false;
            byte[] allowanceKey = GetStorageKey(PREFIX_APPROVE, from, originator);
            if (allowanceKey.Length != 41) return false;
            BigInteger allowanceValue = Storage.Get(Storage.CurrentContext, allowanceKey).AsBigInteger();
            if (Compare(allowanceValue, value) < 0) return false;

            BigInteger fromValue = GetBalance(from);
            if (Compare(fromValue, value) < 0) return false;

            byte[] fromKey = GetStorageKey(PREFIX_BALANCE, from);
            WorkingWithStorage(fromKey, fromValue, value);

            BigInteger toValue = GetBalance(to);
            byte[] toKey = GetStorageKey(PREFIX_BALANCE, to);
            Storage.Put(Storage.CurrentContext, toKey, toValue + value);

            WorkingWithStorage(allowanceKey, allowanceValue, value);

            Transferred(from, to, value);
            return true;
        }

        // This method overwrites the any value
        public static bool Approve(byte[] owner, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(owner)) return false;
            byte[] approvalKey = GetStorageKey(PREFIX_APPROVE, owner, to);
            if (approvalKey.Length != 41) return false;
            Storage.Put(Storage.CurrentContext, approvalKey, value);
            Approved(owner, to, value);
            return true;
        }

        // Get the amount that `to` can send on behalf of `owner`
        public static BigInteger Allowance(byte[] owner, byte[] to)
        {
            if (owner.Length != 20) return -1;
            if (to.Length != 20) return -1;
            byte[] allowanceKey = GetStorageKey(PREFIX_APPROVE, owner, to);
            return Storage.Get(Storage.CurrentContext, allowanceKey).AsBigInteger();
        }

        // Get the balance of a address
        public static BigInteger BalanceOf(byte[] address)
        {
            byte[] key = GetStorageKey(PREFIX_BALANCE, address);
            return Storage.Get(Storage.CurrentContext, key).AsBigInteger();
        }

        // Check whether asset is neo and get sender script hash
        private static byte[] GetSender()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] reference = tx.GetReferences();
            // you can choice refund or not refund
            foreach (TransactionOutput output in reference)
            {
                if (output.AssetId == NEO_ASSET_ID) return output.ScriptHash;
            }

            return new byte[0];
        }

        // Send `amount` of tokens for `to` an address that are locked until `time`
        public static bool Lock(byte[] from, byte[] to, BigInteger value, BigInteger lockTime)
        {
            if (value <= 0) return false;
            if (to.Length != 20) return false;
            if (from.Length != 20) return false;
            if ((uint)lockTime <= Runtime.Time) return false;
            if (!Runtime.CheckWitness(from)) return false;

            BigInteger fromValue = GetBalance(from);
            if (Compare(fromValue, value) < 0) return false;
            byte[] fromKey = GetStorageKey(PREFIX_BALANCE, from);

            WorkingWithStorage(fromKey, fromValue, value);

            byte[] lockKey = GetStorageKey(PREFIX_LOCK, to, lockTime.ToByteArray());
            BigInteger lockValue = Storage.Get(Storage.CurrentContext, lockKey).AsBigInteger();
            Storage.Put(Storage.CurrentContext, lockKey, lockValue + value);

            Locked(from, to, value, lockTime);
            return true;
        }

        // Unlock all tokens locked for `to` at `time`
        public static bool Unlock(byte[] to, BigInteger time)
        {
            if ((uint)time > Runtime.Time) return false;
            if (to.Length != 20) return false;

            byte[] lockKey = GetStorageKey(PREFIX_LOCK, to, time.ToByteArray());
            BigInteger value = Storage.Get(Storage.CurrentContext, lockKey).AsBigInteger();

            if (value == 0) return false;

            BigInteger toValue = GetBalance(to);
            byte[] toKey = GetStorageKey(PREFIX_BALANCE, to);
            Storage.Put(Storage.CurrentContext, toKey, toValue + value);
            Storage.Delete(Storage.CurrentContext, lockKey);

            Unlocked(to, value, time);
            return true;
        }


        public static int WhitelistRegister(params object[] args)
        {


            if (!Helper.IsOwner())
            {
                return 0;
            }

            int savedAddressesCount = 0;

            foreach (byte[] address in args)
            {
                if (Helper.IsValidAddress(address))
                {
                    Storage.Put(Storage.CurrentContext, Helper.StorageKey(WHITELISTED_KEY, address), 1);

                    OnWhitelistRegister(address);
                    savedAddressesCount = savedAddressesCount + 1;
                }
            }

            return savedAddressesCount;
        }

        public static bool IsWhitelisted(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, Helper.StorageKey(WHITELISTED_KEY, address)).AsBigInteger() == 1;
        }

        static bool AirdropTokens(byte[] address, BigInteger amount)
        {
            if (!Helper.IsOwner())
            {
                return false;
            }

            if (!Helper.IsValidAddress(address))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            BigInteger newAmount = TotalSupply() + amount;

            // Check if not going over max supply
            if (newAmount > TOKEN_MAX_SUPPLY)
            {
                return false;
            }

            BigInteger currentBalance = BalanceOf(address);

            BigInteger newTotal = currentBalance + amount;

            // Update balance
            Storage.Put(Storage.CurrentContext, Helper.StorageKey(BALANCE_KEY, address), newTotal);

            // Update total supply
            AddToTotalSupply(newTotal);

            Transferred(null, address, amount);

            return true;
        }


        // Get smart contract script hash
        private static byte[] GetReceiver()
        {
            return ExecutionEngine.ExecutingScriptHash;
        }

        // Get all you contribute neo amount
        private static ulong GetContributeValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;

            // get the total amount of Neo
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == GetReceiver() && output.AssetId == NEO_ASSET_ID)
                {
                    value += (ulong)output.Value;
                }
            }

            return value;
        }

        // Generate storage key using preffix
        public static byte[] GetStorageKey(string prefix, params byte[][] args)
        {
            byte[] prefixArray = prefix.AsByteArray();
            byte[] key = prefixArray.Concat(args[0]);
            for (int i = 1; i < args.Length; ++i)
                key = key.Concat(args[i]);
            return key;
        }

        // Get token balance for an address
        public static BigInteger GetBalance(byte[] address)
        {
            byte[] key = GetStorageKey(PREFIX_BALANCE, address);
            return new BigInteger(Storage.Get(Storage.CurrentContext, key));
        }

        // Substract or Delete info from storage
        public static void WorkingWithStorage(byte[] key, BigInteger oldValue, BigInteger subValue)
        {
            if (oldValue == subValue)
                Storage.Delete(Storage.CurrentContext, key);
            else
                Storage.Put(Storage.CurrentContext, key, oldValue - subValue);
        }

        public static BigInteger LockedBalance(byte[] address, BigInteger time)
        {
            if (address.Length != 20) return 0;
            byte[] lockKey = GetStorageKey(PREFIX_LOCK, address, time.ToByteArray());
            return Storage.Get(Storage.CurrentContext, lockKey).AsBigInteger();
        }

        private static int Compare(BigInteger current, BigInteger other)
        {
            if (current == other) return 0;
            if (current > other) return 1;
            return -1;
        }

       
    }
}
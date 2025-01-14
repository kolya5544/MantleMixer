using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Numerics;

namespace MantleMixer
{
    internal class Program
    {
        public static HexBigInteger gasEstimate = new(180_000_000);
        public static HexBigInteger gasPrice;
        public static BigInteger gasCostInWeiL1;
        public static BigInteger gasCostInWeiL2;

        public static Web3 web3;
        public static string mantleRpcUrl = "https://rpc.sepolia.mantle.xyz";

        static async Task Main(string[] args)
        {
            // Create a new wallet and private key
            var account = NewAccount();

            // Get the public address
            var publicAddress = account.Address;
            Console.WriteLine($"Send money to this address -> {publicAddress}");

            // Ask the user for a TX Hash
            Console.Write("Enter the transaction hash after sending the funds: ");
            string txHash = Console.ReadLine();

            Console.Write($"Enter the wallet that you want to receive said money: ");
            string recipient = Console.ReadLine();

            // Connect to the Mantle network (replace with Mantle's RPC endpoint)
            web3 = new Web3(account, mantleRpcUrl);

            gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
            gasCostInWeiL1 = gasEstimate.Value * gasPrice.Value;
            gasCostInWeiL2 = gasCostInWeiL1 / 100 * 119;

            try
            {
                // Get the transaction details
                var transaction = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);

                if (transaction != null)
                {
                    // Check if the transaction was sent to our wallet
                    if (transaction.To != null && transaction.To.Equals(publicAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        // this is the value we'll want to get on the other end.
                        var valueInWei = transaction.Value.Value;
                        var valueInMNT = Web3.Convert.FromWei(valueInWei);

                        Console.WriteLine($"Received {valueInMNT} MNT from TX Hash: {txHash}. Processing...");

                        // now, according to the scheme
                        // D1 -> 2 acct
                        var amtSplt = (valueInWei / 2).ToHexBigInteger();
                        var d11 = NewAccount();
                        var d12 = NewAccount();
                        var d11_tx = await SendTransactionAsync(account, d11, amtSplt);
                        var d12_tx = await SendTransactionAsync(account, d12, amtSplt);

                        Console.WriteLine($"[D1 COMPLETE] Sent {Web3.Convert.FromWei(amtSplt)} MNT to {d11.Address} & {d12.Address}");

                        // D2, each account to 3 acct
                        var amtSpltD2 = (amtSplt.Value / 3).ToHexBigInteger();
                        var d21 = NewAccount();
                        var d22 = NewAccount();
                        var d23 = NewAccount();
                        var d24 = NewAccount();
                        var d25 = NewAccount();
                        var d26 = NewAccount();

                        var d21_tx = await SendTransactionAsync(d11, d21, amtSpltD2);
                        var d22_tx = await SendTransactionAsync(d11, d22, amtSpltD2);
                        var d23_tx = await SendTransactionAsync(d11, d23, amtSpltD2);
                        var d24_tx = await SendTransactionAsync(d12, d24, amtSpltD2);
                        var d25_tx = await SendTransactionAsync(d12, d25, amtSpltD2);
                        var d26_tx = await SendTransactionAsync(d12, d26, amtSpltD2);

                        Console.WriteLine($"[D2 COMPLETE] Sent {Web3.Convert.FromWei(amtSpltD2)} MNT to:");
                        Console.WriteLine($">  {d21.Address}");
                        Console.WriteLine($">  {d22.Address}");
                        Console.WriteLine($">  {d23.Address}");
                        Console.WriteLine($">  {d24.Address}");
                        Console.WriteLine($">  {d25.Address}");
                        Console.WriteLine($">  {d26.Address}");

                        // R1, reaccumulate from D2 into three total accounts
                        var r11 = NewAccount();
                        var r12 = NewAccount();
                        var r13 = NewAccount();

                        var r11_tx = await SendTransactionAsync(d21, r11, amtSpltD2);
                        var r12_tx = await SendTransactionAsync(d22, r11, amtSpltD2);
                        var r13_tx = await SendTransactionAsync(d23, r12, amtSpltD2);
                        var r14_tx = await SendTransactionAsync(d24, r12, amtSpltD2);
                        var r15_tx = await SendTransactionAsync(d25, r13, amtSpltD2);
                        var r16_tx = await SendTransactionAsync(d26, r13, amtSpltD2);

                        Console.WriteLine($"[R1 COMPLETE] Sent {Web3.Convert.FromWei(amtSpltD2)} MNT to:");
                        Console.WriteLine($">  {r11.Address}");
                        Console.WriteLine($">  {r12.Address}");
                        Console.WriteLine($">  {r13.Address}");

                        // S1, stealth operation that shuffles between R1 nodes, and two stealth nodes
                        // in this order
                        // R11 -> R12 (amtSpltD2 * 2)
                        // R13 -> R12 (amtSpltD2 * 2)

                        // R12 -> S11 (amtSpltD2 * 2)
                        // R12 -> S12 (amtSpltD2 * 2)

                        // S11 -> R13 (amtSpltD2 * 2)
                        // S12 -> R11 (amtSpltD2 * 2)

                        var amt = (amtSpltD2.Value * 2).ToHexBigInteger();

                        var s11 = NewAccount();
                        var s12 = NewAccount();

                        var a = await SendTransactionAsync(r11, r12, amt);
                        var b = await SendTransactionAsync(r13, r12, amt);
                        var c = await SendTransactionAsync(r12, s11, amt);
                        var d = await SendTransactionAsync(r12, s12, amt);
                        var e = await SendTransactionAsync(s11, r13, amt);
                        var f = await SendTransactionAsync(s12, r11, amt);

                        Console.WriteLine($"[S1 COMPLETE] Reshuffled. Secret addresses: {s11.Address} & {s12.Address}");

                        // R2, reaccumulate from R11, R12, and R13 into three total accounts
                        var r21 = NewAccount();
                        var r22 = NewAccount();
                        var r23 = NewAccount();

                        var r21_tx = await SendTransactionAsync(r13, r21, amt);
                        var r23_tx = await SendTransactionAsync(r11, r23, amt);
                        var r22_tx = await SendTransactionAsync(r12, r22, amt);

                        Console.WriteLine($"[R2 COMPLETE] Sent {Web3.Convert.FromWei(amt)} MNT to:");
                        Console.WriteLine($">  {r21.Address}");
                        Console.WriteLine($">  {r22.Address}");
                        Console.WriteLine($">  {r23.Address}");

                        // R3, reaccumulate from R21, R22, and R23 into three total accounts
                        // R21 -> R31 (amt)
                        // R23 -> R33 (amt)

                        // R22 -> R31 (amt / 3)
                        // R22 -> R32 (amt / 3)
                        // R22 -> R33 (amt / 3)

                        var amt3 = (amt.Value / 3).ToHexBigInteger();

                        var r31 = NewAccount();
                        var r32 = NewAccount();
                        var r33 = NewAccount();

                        var r31_tx = await SendTransactionAsync(r21, r31, amt);
                        var r33_tx = await SendTransactionAsync(r23, r33, amt);

                        var r32_tx = await SendTransactionAsync(r22, r31, amt3);
                        var r32_tx_2 = await SendTransactionAsync(r22, r32, amt3);
                        var r32_tx_3 = await SendTransactionAsync(r22, r33, amt3);

                        Console.WriteLine($"[R3 COMPLETE] Sent {Web3.Convert.FromWei(amt)} MNT to:");
                        Console.WriteLine($">  {r31.Address}");
                        Console.WriteLine($">  {r32.Address}");
                        Console.WriteLine($">  {r33.Address}");

                        // C, culmination, send all of R3 to C
                        var c1 = NewAccount();

                        var amt31 = (amt3.Value + amt.Value).ToHexBigInteger();

                        var c1_tx_1 = await SendTransactionAsync(r31, c1, amt31);
                        var c1_tx_2 = await SendTransactionAsync(r32, c1, amt3);
                        var c1_tx_3 = await SendTransactionAsync(r33, c1, amt31);

                        Console.WriteLine($"[C COMPLETE] Sent {Web3.Convert.FromWei(valueInWei)} MNT to:");
                        Console.WriteLine($">  {c1.Address}");

                        // E, send from C to recipient
                        var e_tx = await SendTransactionAsync(c1, recipient, valueInWei.ToHexBigInteger());
                    }
                    else
                    {
                        Console.WriteLine("The transaction hash provided does not point to this wallet.");
                    }
                }
                else
                {
                    Console.WriteLine("Transaction not found. Please check the transaction hash.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        public static async Task<TransactionReceipt> SendTransactionAsync(Account from, Account to, HexBigInteger amount) => await SendTransactionAsync(from, to.Address, amount);
        public static async Task<TransactionReceipt> SendTransactionAsync(Account from, string to, HexBigInteger amount)
        {
            web3 = new Web3(from, mantleRpcUrl);

            // check balance before sending, clamp to balance
            var balance = await web3.Eth.GetBalance.SendRequestAsync(from.Address);
            if (amount > balance.Value)
                amount = balance;

            var ti = new TransactionInput("", to, from.Address, gasEstimate, (amount - gasCostInWeiL1 - gasCostInWeiL2).ToHexBigInteger());

            var transactionReceipt = await web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(
                ti
                );

            return transactionReceipt;
        }

        public static Account NewAccount()
        {
            var privateKey = Nethereum.Signer.EthECKey.GenerateKey().GetPrivateKey();
            return new Account(privateKey);
        }
    }
}

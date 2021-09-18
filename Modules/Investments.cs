﻿using Discord;
using Discord.Commands;
using RRBot.Entities;
using RRBot.Extensions;
using RRBot.Preconditions;
using RRBot.Systems;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace RRBot.Modules
{
    [Summary("Invest in our selection of coins, Bit or Shit. The prices here are updated in REAL TIME with the REAL LIFE values. Experience the fast, entrepreneural life without going broke, having your house repossessed, and having your girlfriend leave you.")]
    public class Investments : ModuleBase<SocketCommandContext>
    {
        public CultureInfo CurrencyCulture { get; set; }

        public static string ResolveAbbreviation(string crypto)
        {
            return crypto.ToLower() switch
            {
                "bitcoin" or "btc" => "BTC",
                "dogecoin" or "doge" => "DOGE",
                "ethereum" or "eth" => "ETH",
                "litecoin" or "ltc" => "LTC",
                "xrp" => "XRP",
                _ => null,
            };
        }

        [Command("invest")]
        [Summary("Invest in a cryptocurrency. Currently accepted currencies are BTC, DOGE, ETH, LTC, and XRP. Here, the amount you put in should be RR Cash.")]
        [Remarks("$invest [crypto] [amount]")]
        [RequireCash]
        public async Task<RuntimeResult> Invest(string crypto, double amount)
        {
            if (amount < 0 || double.IsNaN(amount))
                return CommandResult.FromError("You can't invest nothing!");

            string abbreviation = ResolveAbbreviation(crypto);
            if (abbreviation is null)
                return CommandResult.FromError($"**{crypto}** is not a currently accepted currency!");

            DbUser user = await DbUser.GetById(Context.Guild.Id, Context.User.Id);
            double cryptoAmount = amount / await CashSystem.QueryCryptoValue(abbreviation);

            if (user.Cash < amount)
            {
                return CommandResult.FromError("You can't invest more than what you have!");
            }
            if (cryptoAmount < Constants.INVESTMENT_MIN_AMOUNT)
            {
                return CommandResult.FromError($"The amount you specified converts to less than {Constants.INVESTMENT_MIN_AMOUNT} of {abbreviation}, which is not permitted.\n"
                    + $"You'll need to invest at least **{await CashSystem.QueryCryptoValue(abbreviation) * Constants.INVESTMENT_MIN_AMOUNT:C2}**.");
            }

            await user.SetCash(Context.User, Context.Channel, user.Cash - amount);
            user[abbreviation] = Math.Round(amount, 4);
            user.AddToStats(CurrencyCulture, new Dictionary<string, string>
            {
                { $"Money Put Into {abbreviation}", amount.ToString("C2", CurrencyCulture) },
                { $"{abbreviation} Purchased", cryptoAmount.ToString("0.####") }
            });

            await user.Write();
            await Context.User.NotifyAsync(Context.Channel, $"You have invested in **{cryptoAmount:0.####}** {abbreviation}, currently valued at **{amount:C2}**.");
            return CommandResult.FromSuccess();
        }

        [Command("investments")]
        [Summary("Check your investments, or someone else's, and their value.")]
        [Remarks("$investments <user>")]
        public async Task<RuntimeResult> InvestmentsView(IGuildUser user = null)
        {
            if (user?.IsBot == true)
                return CommandResult.FromError("Nope.");

            ulong userId = user == null ? Context.User.Id : user.Id;
            DbUser dbUser = await DbUser.GetById(Context.Guild.Id, userId);

            StringBuilder investments = new();
            if (dbUser.BTC >= Constants.INVESTMENT_MIN_AMOUNT)
                investments.AppendLine($"**Bitcoin (BTC)**: {dbUser.BTC:0.####} ({await CashSystem.QueryCryptoValue("BTC") * dbUser.BTC:C2})");
            if (dbUser.DOGE >= Constants.INVESTMENT_MIN_AMOUNT)
                investments.AppendLine($"**Dogecoin (DOGE)**: {dbUser.DOGE:0.####} ({await CashSystem.QueryCryptoValue("DOGE") * dbUser.DOGE:C2})");
            if (dbUser.ETH >= Constants.INVESTMENT_MIN_AMOUNT)
                investments.AppendLine($"**Ethereum (ETH)**: {dbUser.ETH:0.####} ({await CashSystem.QueryCryptoValue("ETH") * dbUser.ETH:C2})");
            if (dbUser.LTC >= Constants.INVESTMENT_MIN_AMOUNT)
                investments.AppendLine($"**Litecoin (LTC)**: {dbUser.LTC:0.####} ({await CashSystem.QueryCryptoValue("LTC") * dbUser.LTC:C2})");
            if (dbUser.XRP >= Constants.INVESTMENT_MIN_AMOUNT)
                investments.AppendLine($"**XRP**: {dbUser.XRP:0.####} ({await CashSystem.QueryCryptoValue("XRP") * dbUser.XRP:C2})");

            EmbedBuilder embed = new()
            {
                Color = Color.Red,
                Title = user == null ? "Your Investments" : $"{user}'s Investments",
                Description = investments.Length > 0 ? investments.ToString() : "None"
            };

            await ReplyAsync(embed: embed.Build());
            return CommandResult.FromSuccess();
        }

        [Alias("values")]
        [Command("prices")]
        [Summary("Check the values of currently available cryptocurrencies.")]
        [Remarks("$prices")]
        public async Task Prices()
        {
            double btc = await CashSystem.QueryCryptoValue("BTC");
            double doge = await CashSystem.QueryCryptoValue("DOGE");
            double eth = await CashSystem.QueryCryptoValue("ETH");
            double ltc = await CashSystem.QueryCryptoValue("LTC");
            double xrp = await CashSystem.QueryCryptoValue("XRP");

            EmbedBuilder embed = new()
            {
                Color = Color.Red,
                Title = "Cryptocurrency Values",
                Description = $"**Bitcoin (BTC)**: {btc:C2}\n**Dogecoin (DOGE)**: {doge:C2}\n**Ethereum (ETH)**: {eth:C2}" +
                    $"\n**Litecoin (LTC)**: {ltc:C2}\n**XRP**: {xrp:C2}"
            };

            await ReplyAsync(embed: embed.Build());
        }

        [Command("withdraw")]
        [Summary("Withdraw a specified cryptocurrency to RR Cash, with a 2% withdrawal fee. Here, the amount you put in should be in the crypto, not RR Cash. See $invest's help info for currently accepted currencies.")]
        [Remarks("$withdraw [crypto] [amount]")]
        public async Task<RuntimeResult> Withdraw(string crypto, double amount)
        {
            if (amount < Constants.INVESTMENT_MIN_AMOUNT || double.IsNaN(amount))
                return CommandResult.FromError($"You must withdraw {Constants.INVESTMENT_MIN_AMOUNT} or more of the crypto!");

            string abbreviation = ResolveAbbreviation(crypto);
            if (abbreviation is null)
                return CommandResult.FromError($"**{crypto}** is not a currently accepted currency!");

            DbUser user = await DbUser.GetById(Context.Guild.Id, Context.User.Id);
            double cryptoBal = (double)user[abbreviation];
            if (cryptoBal < Constants.INVESTMENT_MIN_AMOUNT)
                return CommandResult.FromError($"You have no {abbreviation}!");
            if (cryptoBal < amount)
                return CommandResult.FromError($"You don't have {amount} {abbreviation}! You've only got **{cryptoBal}** of it.");

            double cryptoValue = await CashSystem.QueryCryptoValue(abbreviation) * amount;
            double finalValue = cryptoValue / 100.0 * (100 - Constants.INVESTMENT_FEE_PERCENT);

            await user.SetCash(Context.User, Context.Channel, user.Cash + finalValue);
            user[abbreviation] = Math.Round(-amount, 4);
            user.AddToStats(CurrencyCulture, new Dictionary<string, string>
            {
                { $"Money Gained From {abbreviation}", finalValue.ToString("C2", CurrencyCulture) }
            });

            await user.Write();
            await Context.User.NotifyAsync(Context.Channel, $"You have withdrew **{amount}** {abbreviation}, currently valued at **{cryptoValue:C2}**.\n" +
                $"A {Constants.INVESTMENT_FEE_PERCENT}% withdrawal fee was taken from this amount, leaving you **{finalValue:C2}** richer.");
            return CommandResult.FromSuccess();
        }
    }
}

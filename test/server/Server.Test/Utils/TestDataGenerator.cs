﻿using AlfaBank.Core.Extensions;
using AlfaBank.Core.Infrastructure;
using AlfaBank.Core.Models;
using AlfaBank.Core.Models.Dto;
using AlfaBank.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Server.Test.Utils
{
    [ExcludeFromCodeCoverage]
    public class TestDataGenerator : ITestDataGenerator
    {
        private readonly ICardService _cardService;
        private readonly ICardNumberGenerator _cardNumberGenerator;

        private IEnumerable<Card> _cards;

        public TestDataGenerator(ICardService cardService, ICardNumberGenerator cardNumberGenerator)
        {
            _cardService = cardService ?? throw new ArgumentNullException(nameof(cardService));
            _cardNumberGenerator = cardNumberGenerator ?? throw new ArgumentNullException(nameof(cardNumberGenerator));
        }

        public static User GenerateFakeUser(IEnumerable<Card> cards)
        {
            var user = new User("admin@admin.net", "123") {Cards = new List<Card>()};
            user.Cards.AddRange(cards);

            return user;
        }

        public static IEnumerable<CardGetDto> GenerateFakeCardGetDtoList(IEnumerable<Card> cards) =>
            cards.Select(card => new CardGetDto
            {
                Number = card.CardNumber,
                Type = (int) card.CardType,
                Name = card.CardName,
                Currency = (int) card.Currency,
                Exp = card.DtOpenCard.ToShortStringFormat(card.ValidityYear),
                Balance = card.RoundBalance
            });

        public static CardGetDto GenerateFakeCardGetDto(Card card) =>
            new CardGetDto
            {
                Number = card.CardNumber,
                Type = (int) card.CardType,
                Name = card.CardName,
                Currency = (int) card.Currency,
                Exp = card.DtOpenCard.ToShortStringFormat(card.ValidityYear),
                Balance = card.RoundBalance
            };

        public static CardPostDto GenerateFakeValidityCardDto() =>
            new CardPostDto
            {
                Name = "new cardDto",
                Type = 5,
                Currency = 4
            };

        public static CardPostDto GenerateCardDto() =>
            new CardPostDto
            {
                Name = "new cardDto",
                Type = 1,
                Currency = 0
            };

        public static Transaction GenerateFakeTransaction(Card card, decimal sum = 10) =>
            new Transaction
            {
                Card = card,
                CardFromNumber = card.CardNumber,
                CardToNumber = "4083969259636239",
                Sum = sum
            };

        public static Transaction GenerateFakeTransaction(Card card, TransactionPostDto transaction) =>
            new Transaction
            {
                Card = card,
                CardFromNumber = transaction.From,
                CardToNumber = transaction.To,
                Sum = transaction.Sum
            };

        public static IEnumerable<Transaction> GenerateFakeTransactions(Card card) =>
            Enumerable.Repeat(GenerateFakeTransaction(card), 5);

        public static TransactionPostDto GenerateFakePostTransactionDto(Card cardFrom, Card cardTo) =>
            new TransactionPostDto
            {
                From = cardFrom.CardNumber,
                To = cardTo.CardNumber,
                Sum = 10
            };

        public static TransactionGetDto GenerateFakeGetTransactionDto(Transaction transaction) =>
            new TransactionGetDto
            {
                From = transaction.CardFromNumber,
                To = transaction.CardToNumber.CardNumberWatermark(),
                Sum = transaction.Sum,
                IsCredit = transaction.IsCredit,
                DateTime = transaction.DateTime
            };

        public static IEnumerable<TransactionGetDto> GenerateFakeGetTransactionDtoList(
            IEnumerable<Transaction> transactions) =>
            transactions.Select(transaction => new TransactionGetDto
            {
                From = transaction.CardFromNumber,
                To = transaction.CardToNumber.CardNumberWatermark(),
                Sum = transaction.Sum,
                IsCredit = transaction.IsCredit,
                DateTime = transaction.DateTime
            });

        public User GenerateFakeUser() => new User("admin@admin.net", "123");

        public Card GenerateFakeCard(CardPostDto cardDto)
        {
            var card = new Card
            {
                Transactions = new List<Transaction>(),
                CardNumber = _cardNumberGenerator.GenerateNewCardNumber(CardType.MAESTRO),
                CardName = cardDto.Name,
                Currency = (Currency) cardDto.Currency,
                CardType = (CardType) cardDto.Type,
                DtOpenCard = DateTime.Parse("01-01-2019")
            };
            _cardService.TryAddBonusOnOpen(card);

            return card;
        }

        public Card GenerateFakeCard(bool addBonus = true) =>
            GenerateFakeCard(GenerateFakeCardNumber(), addBonus);

        public Card GenerateFakeCard(User user, bool addBonus = true) =>
            GenerateFakeCard(user, GenerateFakeCardNumber(), addBonus);

        public Card GenerateFakeCard(User user, string number, bool addBonus = true)
        {
            var card = GenerateFakeCard(number, addBonus);

            card.User = user;

            return card;
        }

        private Card GenerateFakeCard(string number, bool addBonus = true)
        {
            var card = new Card
            {
                Id = -1,
                Transactions = new List<Transaction>(),
                CardNumber = number,
                CardName = "my cardDto",
                Currency = Currency.RUR,
                CardType = CardType.MAESTRO,
                DtOpenCard = DateTime.Today.AddYears(-1)
            };

            if (addBonus)
            {
                _cardService.TryAddBonusOnOpen(card);
            }

            return card;
        }

        public Card GenerateFakeValidityCard() =>
            GenerateFakeValidityCard(GenerateFakeCardNumber());

        public IEnumerable<Card> GenerateFakeCards()
        {
            var date = DateTime.Parse("01-01-2019");
            if (_cards != null)
            {
                return _cards;
            }

            _cards = new List<Card>
            {
                new Card
                {
                    Transactions = new List<Transaction>(),
                    CardNumber = _cardNumberGenerator.GenerateNewCardNumber(CardType.MAESTRO),
                    CardName = "my salary",
                    Currency = Currency.RUR,
                    CardType = CardType.MAESTRO,
                    DtOpenCard = date
                },
                new Card
                {
                    Transactions = new List<Transaction>(),
                    CardNumber = _cardNumberGenerator.GenerateNewCardNumber(CardType.VISA),
                    CardName = "my debt",
                    Currency = Currency.EUR,
                    CardType = CardType.VISA,
                    DtOpenCard = date
                },
                new Card
                {
                    Transactions = new List<Transaction>(),
                    CardNumber = _cardNumberGenerator.GenerateNewCardNumber(CardType.MASTERCARD),
                    CardName = "my my lovely wife",
                    Currency = Currency.USD,
                    CardType = CardType.MASTERCARD,
                    DtOpenCard = date
                }
            };

            foreach (var card in _cards)
            {
                _cardService.TryAddBonusOnOpen(card);
            }

            return _cards;
        }

        private Card GenerateFakeValidityCard(string number)
        {
            var card = GenerateFakeCard(number);
            card.DtOpenCard = DateTime.Today.AddYears(-6);
            return card;
        }

        private string GenerateFakeCardNumber() => _cardNumberGenerator.GenerateNewCardNumber(CardType.MASTERCARD);
    }
}
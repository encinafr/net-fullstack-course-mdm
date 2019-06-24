﻿using AlfaBank.Core.Data.Interfaces;
using AlfaBank.Core.Exceptions;
using AlfaBank.Core.Infrastructure;
using AlfaBank.Core.Models;
using AlfaBank.Core.Models.Dto;
using AlfaBank.Core.Models.Factories;
using AlfaBank.Services.Checkers;
using AlfaBank.Services.Interfaces;
using AlfaBank.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Server.Test.Mocks;
using Server.Test.Mocks.Data;
using Server.Test.Mocks.Services;
using Server.Test.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable ImplicitlyCapturedClosure
namespace Server.Test.Controllers
{
    public class CardsControllerTest : ControllerTestBase, IDisposable
    {
        private readonly TestDataGenerator _testDataGenerator;

        // Mocks and Fakes
        private readonly IEnumerable<Card> _fakeCards;
        private readonly IEnumerable<CardGetDto> _fakeCardsGetDtoList;
        private readonly User _user;
        private bool _isUserCall = true;

        private readonly Mock<ICardRepository> _cardRepositoryMock;
        private readonly Mock<ICardChecker> _cardCheckerMock;
        private readonly Mock<IBankService> _bankServiceMock;
        private readonly Mock<IDtoValidationService> _dtoValidationServiceMock;
        private readonly Mock<IDtoFactory<Card, CardGetDto>> _dtoFactoryMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;

        private readonly CardsController _controller;

        public CardsControllerTest()
        {
            _cardCheckerMock = new CardCheckerMockFactory().Mock();
            _dtoValidationServiceMock = new DtoValidationServiceMockFactory().Mock();
            _dtoFactoryMock = new Mock<IDtoFactory<Card, CardGetDto>>();
            var cardService = new CardServiceMockFactory().MockObject();
            var cardNumberGenerator = new CardNumberGeneratorMockFactory().MockObject();

            _testDataGenerator = new TestDataGenerator(cardService, cardNumberGenerator);
            _bankServiceMock = new Mock<IBankService>();

            // testData
            _fakeCards = _testDataGenerator.GenerateFakeCards();
            _fakeCardsGetDtoList = TestDataGenerator.GenerateFakeCardGetDtoList(_fakeCards);
            _user = TestDataGenerator.GenerateFakeUser(_fakeCards);
            _userRepositoryMock = new UserRepositoryMockFactory(_user).Mock();
            _cardRepositoryMock = new CardsRepositoryMockFactory(_user).Mock();

            _userRepositoryMock.Setup(u => u.GetUser("alice@alfabank.ru", It.IsAny<bool>())).Returns(_user);

            var objectValidatorMock = GetMockObjectValidator();

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "alice@alfabank.ru")
            }, "mock"));

            _controller = new CardsController(
                _dtoValidationServiceMock.Object,
                _cardRepositoryMock.Object,
                _userRepositoryMock.Object,
                _cardCheckerMock.Object,
                _bankServiceMock.Object,
                _dtoFactoryMock.Object,
                new Mock<ILogger<CardsController>>().Object)
            {
                ObjectValidator = objectValidatorMock.Object,
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = user
                    }
                }
            };
        }

        [Fact]
        public void GetCards_ValidData_ReturnCorrectListResult()
        {
            var (result, _) = GetCards_ValidData();

            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public void GetCards_ValidData_ReturnOKResult()
        {
            var (_, cards) = GetCards_ValidData();

            Assert.Equal(_fakeCards.Count(), cards.Count());
        }

        [Fact]
        public void GetCards_ValidDate_OutDtoValidationFail_ReturnEmptyListResult()
        {
            var (_, cards) = GetCards_ValidDate_OutDtoValidationFail();

            Assert.Empty(cards);
        }

        [Fact]
        public void GetCards_ValidDate_OutDtoValidationFail_ReturnOkResult()
        {
            var (result, _) = GetCards_ValidDate_OutDtoValidationFail();

            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public void GetCards_UserNotFound_ReturnForbidResult()
        {
            // Arrange
            _userRepositoryMock.Setup(u => u.GetUser(It.IsAny<string>(), true)).Returns((User) null);

            // Act
            var result = (ForbidResult) _controller.Get().Result;

            // Assert
            _cardRepositoryMock.Verify(r => r.GetAllWithTransactions(_user), Times.Never);
            _dtoFactoryMock.Verify(d => d.Map(It.IsAny<Card>(), It.IsAny<Func<CardGetDto, bool>>()), Times.Never);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public void GetCard_ValidData_OutDtoValidationFail_ReturnNotFoundResult()
        {
            // Arrange
            var fakeCard = GetCard_ValidData();
            _dtoFactoryMock.Setup(d => d.Map(fakeCard, It.IsAny<Func<CardGetDto, bool>>()))
                .Returns((CardGetDto) null);

            // Act
            var result = (NotFoundResult) _controller.Get(fakeCard.CardNumber).Result;

            // Assert
            _cardCheckerMock.Verify(r => r.CheckCardEmitter(fakeCard.CardNumber), Times.Once);
            _cardRepositoryMock.Verify(r => r.GetWithTransactions(_user, fakeCard.CardNumber, true), Times.Once);
            _dtoFactoryMock.Verify(d => d.Map(fakeCard, It.IsAny<Func<CardGetDto, bool>>()), Times.Once);

            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public void GetCard_UserNotFound_ReturnForbidResult()
        {
            // Arrange
            var fakeCard = GetCard_ValidData();
            _userRepositoryMock.Setup(u => u.GetUser(It.IsAny<string>(), true)).Returns((User) null);

            // Act
            var result = (ForbidResult) _controller.Get(fakeCard.CardNumber).Result;

            // Assert
            _cardCheckerMock.Verify(r => r.CheckCardEmitter(fakeCard.CardNumber), Times.Once);
            _cardRepositoryMock.Verify(r => r.GetWithTransactions(_user, fakeCard.CardNumber, true), Times.Never);
            _dtoFactoryMock.Verify(d => d.Map(fakeCard, It.IsAny<Func<CardGetDto, bool>>()), Times.Never);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public void GetCard_ValidData_ReturnOKResult()
        {
            // Arrange
            var fakeCard = GetCard_ValidData();

            // Act
            var result = (OkObjectResult) _controller.Get(fakeCard.CardNumber).Result;

            // Assert
            _cardCheckerMock.Verify(r => r.CheckCardEmitter(fakeCard.CardNumber), Times.Once);
            _cardRepositoryMock.Verify(r => r.GetWithTransactions(_user, fakeCard.CardNumber, true), Times.Once);
            _dtoFactoryMock.Verify(d => d.Map(fakeCard, It.IsAny<Func<CardGetDto, bool>>()), Times.Once);

            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public void GetCard_ValidData_ReturnCorrectCard()
        {
            // Arrange
            var fakeCard = GetCard_ValidData();

            // Act
            var card = (CardGetDto) ((OkObjectResult) _controller.Get(fakeCard.CardNumber).Result).Value;

            // Assert
            _cardCheckerMock.Verify(r => r.CheckCardEmitter(fakeCard.CardNumber), Times.Once);
            _cardRepositoryMock.Verify(r => r.GetWithTransactions(_user, fakeCard.CardNumber, true), Times.Once);
            _dtoFactoryMock.Verify(d => d.Map(fakeCard, It.IsAny<Func<CardGetDto, bool>>()), Times.Once);

            Assert.Equal(fakeCard.CardName, card.Name);
            Assert.Equal(fakeCard.CardNumber, card.Number);
            Assert.Equal(3, card.Type);
            Assert.Equal(10M, card.Balance);
            Assert.Equal("01/22", card.Exp);
        }

        [Theory]
        [InlineData("1234 1234 1233 1234")]
        [InlineData("12341233123")]
        [InlineData("5395029009021990")]
        [InlineData("4978588211036789")]
        public void GetCard_InvalidDto_ReturnBadRequest(string cardNumber)
        {
            // Act
            var getResult = _controller.Get(cardNumber);
            var result = (BadRequestObjectResult) getResult.Result;

            // Assert
            _isUserCall = false;
            _cardCheckerMock.Verify(r => r.CheckCardEmitter(cardNumber), Times.Once);
            _cardRepositoryMock.Verify(r => r.GetWithTransactions(_user, cardNumber, true), Times.Never);

            Assert.IsType<BadRequestObjectResult>(getResult.Result);
            Assert.Equal(400, result.StatusCode);
            Assert.Null(getResult.Value);
        }

        [Fact]
        public void GetCard_NotExistCardDto_ReturnNotFoundRequest()
        {
            // Arrange
            var fakeNotExistCard = _testDataGenerator.GenerateFakeCard(_user, "4261147885542592");
            var cardNumber = fakeNotExistCard.CardNumber;

            _cardCheckerMock.Setup(r => r.CheckCardEmitter(cardNumber)).Returns(true);
            _cardRepositoryMock.Setup(r => r.GetWithTransactions(_user, cardNumber, true)).Returns((Card) null);

            // Act
            var getResult = _controller.Get(fakeNotExistCard.CardNumber);
            var result = (NotFoundResult) getResult.Result;

            // Assert
            _userRepositoryMock.VerifyAll();

            _cardCheckerMock.Verify(r => r.CheckCardEmitter(cardNumber), Times.Once);
            _cardRepositoryMock.Verify(r => r.GetWithTransactions(_user, cardNumber, true), Times.Once);

            Assert.IsType<NotFoundResult>(getResult.Result);
            Assert.Equal(404, result.StatusCode);
            Assert.Null(getResult.Value);
        }

        [Fact]
        public void PostCard_UserNotFound_ReturnForbidResult()
        {
            // Arrange
            var cardDto = PostCard_ValidDto();
            _userRepositoryMock.Setup(u => u.GetUser(It.IsAny<string>(), false)).Returns((User) null);

            // Act
            var result = (ForbidResult) _controller.Post(cardDto).Result;

            // Assert
            _dtoValidationServiceMock.Verify(v => v.ValidateOpenCardDto(cardDto), Times.Once);
            _bankServiceMock.Verify(
                v => v.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type),
                Times.Never);
            _cardRepositoryMock.Verify(r => r.Get(_user, It.IsAny<string>()), Times.Never);
            _dtoFactoryMock.Verify(d => d.Map(It.IsAny<Card>(), It.IsAny<Func<CardGetDto, bool>>()), Times.Never);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public void PostCard_ValidDto_ReturnOKResult()
        {
            // Arrange
            var cardDto = PostCard_ValidDto();

            // Act
            var result = (CreatedResult) _controller.Post(cardDto).Result;

            // Assert
            _dtoValidationServiceMock.Verify(v => v.ValidateOpenCardDto(cardDto), Times.Once);
            _bankServiceMock.Verify(
                v => v.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type),
                Times.Once);
            _cardRepositoryMock.Verify(r => r.Get(_user, It.IsAny<string>()), Times.Never);

            Assert.Equal(201, result.StatusCode);
        }

        [Fact]
        public void PostCard_ValidDto_ReturnCorrectOpenedCard()
        {
            // Arrange
            var cardDto = PostCard_ValidDto();

            // Act
            var resultCard = (CardGetDto) ((CreatedResult) _controller.Post(cardDto).Result).Value;

            // Assert
            _dtoValidationServiceMock.Verify(v => v.ValidateOpenCardDto(cardDto), Times.Once);
            _bankServiceMock.Verify(
                v => v.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type),
                Times.Once);
            _cardRepositoryMock.Verify(r => r.GetWithTransactions(_user, It.IsAny<string>(), false), Times.Never);

            Assert.Equal(10, resultCard.Balance);
            Assert.Equal(cardDto.Name, resultCard.Name);
            Assert.NotNull(resultCard.Number);
            Assert.Equal(cardDto.Currency, resultCard.Currency);
            Assert.Equal(cardDto.Type, resultCard.Type);
            Assert.Equal("01/22", resultCard.Exp);
        }

        [Fact]
        public void PostCard_InternalError_ReturnBadRequest()
        {
            // Arrange
            var cardDto = new CardPostDto
            {
                Name = "my card",
                Currency = 0,
                Type = 1
            };

            _dtoValidationServiceMock
                .Setup(m => m.ValidateOpenCardDto(cardDto)).Returns(Enumerable.Empty<CustomModelError>());

            _bankServiceMock
                .Setup(r => r.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type))
                .Returns(
                    (null, new List<CustomModelError>
                    {
                        new CustomModelError
                        {
                            FieldName = "internal",
                            Message = "Add bonus to card failed",
                            LocalizedMessage = "Ошибка при открытии карты"
                        }
                    }));

            // Act
            var result = (BadRequestObjectResult) _controller.Post(cardDto).Result;

            // Assert
            _dtoValidationServiceMock.Verify(v => v.ValidateOpenCardDto(cardDto), Times.Once);
            _bankServiceMock.Verify(
                v => v.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type),
                Times.Once);

            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public void PostCard_MappingFail_ReturnBadRequest()
        {
            // Arrange
            var cardDto = PostCard_ValidDto();

            _dtoFactoryMock.Setup(d => d.Map(It.IsAny<Card>(), It.IsAny<Func<CardGetDto, bool>>()))
                .Returns((CardGetDto) null);

            // Act
            var result = (BadRequestObjectResult) _controller.Post(cardDto).Result;

            // Assert
            _dtoValidationServiceMock.Verify(v => v.ValidateOpenCardDto(cardDto), Times.Once);
            _bankServiceMock.Verify(
                v => v.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type),
                Times.Once);
            _dtoFactoryMock.Verify(d => d.Map(It.IsAny<Card>(), It.IsAny<Func<CardGetDto, bool>>()), Times.Once);

            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public void PostCard_EmptyName_ReturnBadRequest()
        {
            // Arrange
            var cardDto = new CardPostDto
            {
                Name = string.Empty,
                Currency = 0,
                Type = 1
            };

            var validationResultFake = new List<CustomModelError>
            {
                new CustomModelError
                {
                    FieldName = "name",
                    Message = string.Empty
                }
            };
            _isUserCall = false;

            PostCard_Field_ReturnBadRequest(cardDto, validationResultFake);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(5)]
        public void PostCard_WrongCurrency_ReturnBadRequest(int currency)
        {
            // Arrange
            var cardDto = new CardPostDto
            {
                Name = "name",
                Currency = currency,
                Type = 1
            };

            var validationResultFake = new List<CustomModelError>
            {
                new CustomModelError
                {
                    FieldName = "currency",
                    Message = string.Empty
                }
            };

            _isUserCall = false;
            PostCard_Field_ReturnBadRequest(cardDto, validationResultFake);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(5)]
        public void PostCard_WrongType_ReturnBadRequest(int type)
        {
            // Arrange
            var cardDto = new CardPostDto
            {
                Name = "name",
                Currency = 0,
                Type = type
            };

            var validationResultFake = new List<CustomModelError>
            {
                new CustomModelError
                {
                    FieldName = "currency",
                    Message = string.Empty
                }
            };

            _isUserCall = false;
            PostCard_Field_ReturnBadRequest(cardDto, validationResultFake);
        }

        [Fact]
        public void PutCard_ReturnNotAllowed()
        {
            // Act
            var result = (StatusCodeResult) _controller.Put();
            _isUserCall = false;

            // Assert
            Assert.Equal(405, result.StatusCode);
        }

        [Fact]
        public void DeleteCard_ReturnNotAllowed()
        {
            // Act
            var result = (StatusCodeResult) _controller.Delete();
            _isUserCall = false;

            // Assert
            Assert.Equal(405, result.StatusCode);
        }

        private (OkObjectResult, IEnumerable<CardGetDto>) GetCards_ValidData()
        {
            // Arrange
            _dtoFactoryMock.Setup(d => d.Map(_fakeCards, It.IsAny<Func<CardGetDto, bool>>()))
                .Returns(_fakeCardsGetDtoList);

            // Act
            var result = (OkObjectResult) _controller.Get().Result;
            var cards = (IEnumerable<CardGetDto>) result.Value;

            // Assert
            _cardRepositoryMock.Verify(r => r.GetAllWithTransactions(_user), Times.Once);
            _dtoFactoryMock.Verify(d => d.Map(_fakeCards, It.IsAny<Func<CardGetDto, bool>>()), Times.Once);
            return (result, cards);
        }

        private void PostCard_Field_ReturnBadRequest(
            CardPostDto cardDto,
            IEnumerable<CustomModelError> validationResultFake)
        {
            // Arrange
            _dtoValidationServiceMock.Setup(s => s.ValidateOpenCardDto(cardDto)).Returns(validationResultFake);

            // Act
            var result = (BadRequestObjectResult) _controller.Post(cardDto).Result;

            // Assert
            _dtoValidationServiceMock.Verify(v => v.ValidateOpenCardDto(cardDto), Times.Once);
            _bankServiceMock.Verify(
                v => v.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type),
                Times.Never);

            Assert.Equal(400, result.StatusCode);
        }

        private CardPostDto PostCard_ValidDto()
        {
            var cardDto = new CardPostDto
            {
                Name = "my card",
                Currency = 0,
                Type = 1
            };

            var fakeCard = _testDataGenerator.GenerateFakeCard(cardDto);
            var fakeCardGetDto = TestDataGenerator.GenerateFakeCardGetDto(fakeCard);

            _dtoValidationServiceMock
                .Setup(m => m.ValidateOpenCardDto(cardDto)).Returns(Enumerable.Empty<CustomModelError>());

            _bankServiceMock
                .Setup(r => r.TryOpenNewCard(_user, cardDto.Name, (Currency) cardDto.Currency, (CardType) cardDto.Type))
                .Returns((fakeCard, Enumerable.Empty<CustomModelError>()));

            _dtoFactoryMock.Setup(d => d.Map(fakeCard, It.IsAny<Func<CardGetDto, bool>>()))
                .Returns(fakeCardGetDto);

            return cardDto;
        }

        private Card GetCard_ValidData()
        {
            // Arrange
            var fakeCard = _testDataGenerator.GenerateFakeCard(
                new CardPostDto
                {
                    Name = "my card",
                    Currency = (int) Currency.RUR,
                    Type = (int) CardType.MAESTRO
                });

            var fakeCardGetDto = TestDataGenerator.GenerateFakeCardGetDto(fakeCard);

            _cardCheckerMock.Setup(r => r.CheckCardEmitter(fakeCard.CardNumber)).Returns(true);
            _cardRepositoryMock.Setup(r => r.GetWithTransactions(_user, fakeCard.CardNumber, true)).Returns(fakeCard);
            _dtoFactoryMock.Setup(d => d.Map(fakeCard, It.IsAny<Func<CardGetDto, bool>>()))
                .Returns(fakeCardGetDto);

            return fakeCard;
        }

        private (OkObjectResult, IEnumerable<CardGetDto>) GetCards_ValidDate_OutDtoValidationFail()
        {
            // Arrange
            _dtoFactoryMock.Setup(d => d.Map(_fakeCards, It.IsAny<Func<CardGetDto, bool>>()))
                .Returns(Enumerable.Empty<CardGetDto>());

            // Act
            var result = (OkObjectResult) _controller.Get().Result;
            var cards = (IEnumerable<CardGetDto>) result.Value;

            // Assert
            _cardRepositoryMock.Verify(r => r.GetAllWithTransactions(_user), Times.Once);
            _dtoFactoryMock.Verify(d => d.Map(_fakeCards, It.IsAny<Func<CardGetDto, bool>>()), Times.Once);
            return (result, cards);
        }

        public void Dispose()
        {
            if (_isUserCall)
                _userRepositoryMock.Verify(u => u.GetUser("alice@alfabank.ru", It.IsAny<bool>()), Times.Once);
        }
    }
}
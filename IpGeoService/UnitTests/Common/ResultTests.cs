using Application.Common;
using FluentAssertions;

namespace UnitTests.Common
{
    public class ResultTests
    {
        [Fact]
        public void Success_Should_Set_IsSuccess_And_Value_And_No_Error()
        {
            // Arrange
            const int value = 42;

            // Act
            var result = Result<int>.Success(value);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Value.Should().Be(value);
            result.Error.Should().Be(Error.None);
        }

        [Fact]
        public void Failure_With_Error_Should_Set_IsFailure_And_Error()
        {
            // Arrange
            var error = Error.Validation("Something went wrong");

            // Act
            var result = Result<int>.Failure(error);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be(error);
        }

        [Fact]
        public void Failure_With_Message_And_Type_Should_Create_Error()
        {
            // Act
            var result = Result<int>.Failure("Bad request", ErrorType.Validation);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.IsSuccess.Should().BeFalse();
            result.Error.Message.Should().Be("Bad request");
            result.Error.Type.Should().Be(ErrorType.Validation);
        }
    }
}
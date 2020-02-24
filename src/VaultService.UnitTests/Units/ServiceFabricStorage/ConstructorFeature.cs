using System;
using Microsoft.Extensions.Options;
using Microsoft.ServiceFabric.Data;
using Moq;
using ServiceFabric.Mocks;
using VaultService.S3.Storage;
using Xbehave;
using Xunit;

namespace VaultService.UnitTests.Units.ServiceFabricStorage
{
    public class ConstructorFeature
    {
        private readonly Mock<IOptionsSnapshot<ServiceFabricStorageOptions>> _optionsMock;

        public ConstructorFeature()
        {
            _optionsMock = new Mock<IOptionsSnapshot<ServiceFabricStorageOptions>>();
            _optionsMock.Setup(m => m.Value).Returns(new ServiceFabricStorageOptions { DefaultTimeoutFromSeconds = 10 });
        }

        [Scenario(DisplayName = "Construction of ServiceFabricStorage with valid parameters should succeed")]
        public void Construction_of_ServiceFabricStorage_with_valid_parameters_should_succeed(IReliableStateManager stateManager = null, S3.Storage.ServiceFabricStorage serviceFabricStorage = null)
        {
            $"Given a state manager instance"
                .x(() => stateManager = new MockReliableStateManager());

            $"When the ServiceFabricStorage will be constructed with valid parameters"
                .x(() =>
                {
                    serviceFabricStorage = new S3.Storage.ServiceFabricStorage(stateManager, _optionsMock.Object);
                });

            $"Then the ServiceFabricStorage instance is not null"
                .x(() => Assert.NotNull(serviceFabricStorage));
        }

        [Scenario(DisplayName = "Construction of ServiceFabricStorage without state manager should fail")]
        [Example(null)]
        public void Construction_of_ServiceFabricStorage_without_state_manager_should_fail(IReliableStateManager stateManager, Exception exception = null)
        {
            $"When the ServiceFabricStorage will be constructed with parameter stateManager == null"
                .x(() => exception = Record.Exception(() => new S3.Storage.ServiceFabricStorage(stateManager, _optionsMock.Object)));

            $"Then an ArgumentNullException is thrown"
                .x(() => Assert.IsType<ArgumentNullException>(exception));
        }

        [Scenario(DisplayName = "Construction of ServiceFabricStorage without options should fail")]
        [Example(null)]
        public void Construction_of_ServiceFabricStorage_without_without_options_should_fail(IOptionsSnapshot<ServiceFabricStorageOptions> options, Exception exception = null)
        {
            $"When the ServiceFabricStorage will be constructed with parameter options == null"
                .x(() => exception = Record.Exception(() => new S3.Storage.ServiceFabricStorage(new MockReliableStateManager(), options)));

            $"Then an ArgumentNullException is thrown"
                .x(() => Assert.IsType<ArgumentNullException>(exception));
        }

        [Scenario(DisplayName = "Construction of ServiceFabricStorage with invalid options value should fail")]
        [Example(null)]
        public void Construction_of_ServiceFabricStorage_without_with_invalid_options_value_should_fail(IOptionsSnapshot<ServiceFabricStorageOptions> options, Exception exception = null)
        {
            $"Given options with options.Value == null"
                .x(() =>
                {
                    var optionsMock = new Mock<IOptionsSnapshot<ServiceFabricStorageOptions>>();
                    optionsMock.Setup(m => m.Value).Returns((ServiceFabricStorageOptions)null);
                    options = optionsMock.Object;
                });

            $"When the ServiceFabricStorage will be constructed with parameter options.Value == null"
                .x(() => exception = Record.Exception(() => new S3.Storage.ServiceFabricStorage(new MockReliableStateManager(), options)));

            $"Then an ArgumentNullException is thrown"
                .x(() => Assert.IsType<InvalidOperationException>(exception));
        }
    }
}

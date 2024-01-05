using OpenTelemetry.Proto.Resource.V1;
using TeleBlick.OpenTelemetry;
using TeleBlick.OpenTelemetry.Models;

namespace Tests
{
    public class StorageTests
    {
        private const string SERVICE_NAME_KEY = "service.name";
        private const string SERVICE_NAME_VALUE = "TestApplication";
        private const string INSTANCE_ID_KEY = "service.instance.id";
        private const string INSTANCE_ID_VALUE = "123";
        private const string PROPERTY_KEY = "property.key";
        private const string PROPERTY_VALUE = "TestValue";

        [Fact]
        public void Applications()
        {
            var storage = new TelemetryStorage();
            storage.GetOrAddApplication(GetApplicationResource());
            TelemetryStorage storage2;

            //Write storage to memory stream
            using (var stream = new MemoryStream())
            {
                storage.Write(stream);

                //Read storage from memory stream
                stream.Position = 0;
                storage2 = new TelemetryStorage(stream);
            }

            //Compare storage with storage2
            Assert.Equal(storage.GetApplications().Count, storage2.GetApplications().Count);
            AssertApplicationValues(storage.GetApplications().First());
            AssertApplicationValues(storage2.GetApplications().First());
        }

        private Resource GetApplicationResource()
        {
            var resource = new Resource();
            resource.Attributes.Add(GetKeyValue(SERVICE_NAME_KEY, SERVICE_NAME_VALUE));
            resource.Attributes.Add(GetKeyValue(INSTANCE_ID_KEY, INSTANCE_ID_VALUE));
            resource.Attributes.Add(GetKeyValue(PROPERTY_KEY, PROPERTY_VALUE));
            return resource;
        }

        private OpenTelemetry.Proto.Common.V1.KeyValue GetKeyValue(string key, string value)
        {
            return new OpenTelemetry.Proto.Common.V1.KeyValue
            {
                Key = key,
                Value = new OpenTelemetry.Proto.Common.V1.AnyValue
                {
                    StringValue = value
                }
            };
        }

        private void AssertApplicationValues(Application a)
        {
            Assert.Equal(SERVICE_NAME_VALUE, a.ApplicationName);
            Assert.Equal(INSTANCE_ID_VALUE, a.InstanceId);
            Assert.Equal(3, a.Properties.Count);
            foreach(var p in a.Properties)
            {
                switch (p.Key)
                {
                    case SERVICE_NAME_KEY:
                        Assert.Equal(SERVICE_NAME_VALUE, p.Value);
                        break;
                    case INSTANCE_ID_KEY:
                        Assert.Equal(INSTANCE_ID_VALUE, p.Value);
                        break;
                    case PROPERTY_KEY:
                        Assert.Equal(PROPERTY_VALUE, p.Value);
                        break;
                    default:
                        Assert.Fail($"Unknown property key: {p.Key}");
                        break;
                }
            }
        }
    }
}
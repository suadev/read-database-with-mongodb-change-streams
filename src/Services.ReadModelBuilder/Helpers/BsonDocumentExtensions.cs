using MongoDB.Bson;

namespace Services.ReadModelBuilder.Helpers;

public static class BsonDocumentExtensions
{
    public static IEnumerable<string> GetUpdatedFieldNames(this BsonDocument updatedFields)
    {
        return updatedFields?.Select(f => f.Name) ?? [];
    }

    /// <summary>
    /// Safely extracts a Guid from a BsonValue that could be either a string or binary data.
    /// This handles MongoDB change stream events where _id can be represented in different BSON types.
    /// </summary>
    public static Guid ToGuid(this BsonValue bsonValue)
    {
        if (bsonValue == null)
        {
            throw new ArgumentNullException(nameof(bsonValue), "BsonValue cannot be null when extracting Guid");
        }

        try
        {
            if (bsonValue.IsString)
            {
                return Guid.Parse(bsonValue.AsString);
            }

            if (bsonValue.IsBsonBinaryData)
            {
                var binaryData = bsonValue.AsBsonBinaryData;

                return binaryData.SubType is BsonBinarySubType.UuidLegacy or BsonBinarySubType.UuidStandard
                    ? binaryData.AsGuid
                    : throw new InvalidOperationException($"Binary data has unsupported subtype: {binaryData.SubType}. Expected UuidLegacy or UuidStandard.");
            }

            if (bsonValue.IsObjectId)
            {
                throw new InvalidOperationException("Cannot convert ObjectId to Guid. Document _id should be a UUID (string or binary).");
            }

            throw new InvalidOperationException($"Unsupported BsonType for Guid extraction: {bsonValue.BsonType}. Expected String or BinaryData.");
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Failed to parse Guid from BsonValue. Type: {bsonValue.BsonType}, Value: {bsonValue}", ex);
        }
    }
}

    // ...existing code...

    public virtual async Task<Data.ElectricityPrice?> GetPriceForDateTimeAsync(DateTime dateTime)
    {
        var requestedDate = DateOnly.FromDateTime(dateTime);

        // ...existing code...
    }

# Endrichment API

I completely rewrote the API in a new Azure Function in VS Studio.  We were having odd issues with deploying to Azure from VS Code and I needed to refactor the code anyway.

## POST /api/EnrichData

This endpoint exposes a POST method/operation that allows the client to Enrich the address data for a Missing Person record in SQL.  There is a IsEnriched column if this column is set to **False** for the provided record it will pass the Missing From address to the Azure Maps API which performs a simaliarity search returning Latitude, Longitude some some other data that would be helpful for Missing Persons.  Below is an example of what is returned from the call to Azure Maps API.



   ~~~



## Learn more

<TODO> Documentation

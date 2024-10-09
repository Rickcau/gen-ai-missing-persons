namespace blog_trigger_mp_ingest.Prompts
{
    public static class MissingPersonsPluginPrompts
    {


        public static string GetMissingPersonsExtractPrompt(string textData) =>
        $$$"""
        ###
        PERSONA: You are an expert in missing persons and extracting their data and converting it into JSON.
        
        ###
        PARAMETERS:
        1. Fields: name, race, age, sex, height, weight, eye_color, hair, alias, tattoos, last_seen, date_reported, missing_from, conditions_of_disappearance, officer_info, phone_number1, phone_number2
        2. Data: {{{textData}}}
        
        ###
        STEPS:                 
        1. Extract Fields: Using the fields, extract the details from the Data provided.
        2. Create JSON: Using the JSON struture below, populate the JSON structure with the data extracted from the Data.
        3. Please review JSON data to ensure it is properly formatted and free of errors. Make any necessary corrections for consistency and clarity
        
        ::: IMPORTANT: Return ONLY the raw JSON object. Do not wrap it in backticks or any other formatting. :::

        ###
        ### RESPONSE FORMAT: 
        Ensure the response is a raw JSON object structured as follows. Do not include any additional text, markdown formatting, or code block syntax.
        {
            "name": "",
            "race": "",
            "age": 0,
            "sex": "",
            "height": "",
            "weight": "",
            "eye_color": "",
            "hair": "",
            "alias": "",
            "tattoos": "",
            "last_seen": "",
            "date_reported": "",
            "missing_from": "",
            "conditions_of_disappearance": "",
            "officer_info":"",
            "phone_number1":"",
            "phone_number2":"",
            "latitude":0,
            "longitude":0
        }
        
        ::: EXAMPLE INPUT: :::
        1. NAME: Mike Smith
        2. RACE: Black
        3. AGE: 16
        4. SEX: Male
        5. HEIGHT: 5' 7"
        6. WEIGHT: 170 lbs.
        7. EYE COLOR: Brown
        8. HAIR: Black
        9. ALIAS: N/A
        10. TATTOOS: N/A
        11. LAST SEEN: 05/19/2024
        12. DATE REPORTED: 05/19/2024
        13. MISSING FROM: 3900 block of Berkshire
        14. CONDITIONS OF DISAPPEREARANCE: Keyshawn left his residence without permission and failed to return home. He was last seen wearing a a white shirt.
        15. OFFICER INFO: COMMANDER JEVON JOHNSON 5th PRECINCT
        16. PHONE NUMBER1: 313-596-5540
        17. PHONE NUMBER2: 1-800-SPEAKUP
        18. LATITUDE: 0
        19. LONGITUDE: 0

        ::: EXAMPLE OUTPUT: :::
        {
            "name": "Mike Smith",
            "race": "Black",
            "age": 16,
            "sex": "Male"n,
            "height": "5' 7\"",
            "weight": "170 lbs.",
            "eye_color": "Brown",
            "hair": "Black",
            "alias": "N/A",
            "tattoos": "N/A",
            "last_seen": "05/19/2024",
            "date_reported": "05/19/2024",
            "missing_from": "3900 block of Berkshire",
            "conditions_of_disappearance": "Keyshawn left his residence without permission and failed to return home. He was last seen wearing a a white shirt.",
            "officer_info":"Keyshawn left his residence without permission and failed to return home. He was last seen wearing a a white shirt.",
            "phone_number1":"313-596-5540",
            "phone_number2":"1-800-SPEAKUP",
            "latitude":0,
            "longitude":0
        }

        ::: RETURN the extracted data as JSON ::: 
       """;

       public static string GetLatitudeLongitudePrompt(string missingFrom) =>
        $$$"""
        ###
        Instructions: Extract the geo location for {{{missingFrom}}} in Detroit. Return the result using the raw JSON structure {"latitude": 0.0, "longitude": 0.0}.
        ###
        ::: IMPORTANT: Return ONLY the raw JSON object. Do not wrap it in backticks or any other formatting. :::
       """;
    }
}
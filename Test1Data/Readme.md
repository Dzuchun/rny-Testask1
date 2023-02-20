Before testing/using the service, please do the following:
1) duplicate "App.config.template" file
2) strip .template part of it's name
3) define A and B folder locations inside it, were shown

This service allows to instantly process files newly created in A folder and save the result to B folder subfolder.
The actual processing involves some grouping and arrgegation. The result is a properly formatted JSON file.

The processing speed it VERY varying. To my opinion, that is due to threds used to parallel LINQ struggle processing multiple files at the time.
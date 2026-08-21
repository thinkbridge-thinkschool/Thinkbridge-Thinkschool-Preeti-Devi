Baseline Profile



Endpoint:



'GET /api/quotes/slow-authors'



Load Test



Tool: k6



Configuration:

\- 10 virtual users

\- 30 seconds

\- 2,590 requests

\- 86.10 requests/second

\- 0% HTTP failures



Latency:

\- p50: 114.75 ms

\- p99: 155.99 ms

\- p95: 127.29 ms

\- max: 210.44 ms



Offending SQL



The endpoint produces an N+1 query pattern.



The author query is executed once, followed by a quote query for each author:



sql

SELECT "q"."Id", "q"."Author", "q"."AuthorId", "q"."Text"

FROM "Quotes" AS "q"

WHERE "q"."AuthorId" = @author\_Id

ORDER BY "q"."Id";

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



Optimization Results



&#x20;Before



\- p50: 114.75 ms

\- p99: 155.99 ms

\- Execution plan: 'SCAN q'

\- Problem: N+1 query pattern and missing 'AuthorId' index.



Changes Made



1\. Eliminated the N+1 query pattern using an EF Core projection.

2\. Added an index on 'Quotes.AuthorId'.

3\. Re-ran the same k6 load test with 10 VUs for 30 seconds.



After



\- p50: 2.36 ms

\- p99: 4.57 ms

\- HTTP failures: 0%

\- Execution plan: 'SEARCH q USING INDEX IX\_Quotes\_AuthorId (AuthorId=?)'



Improvement



p99 improved from 155.99 ms → 4.57 ms, which is approximately a 34.1× improvement.



The required 10× improvement target was achieved.


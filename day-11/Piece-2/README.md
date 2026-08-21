Day 11 — Piece 2: Drop p99 by 10×



Objective



Fix the slow '/api/quotes/slow-authors' endpoint by:



1\. Eliminating the N+1 query pattern.

2\. Adding the correct database index.

3\. Re-running the same load test.

4\. Comparing before/after execution plans and latency.



Baseline



The original endpoint used an N+1 query pattern.



With 100 authors:



\- 1 query loaded the authors.

\- Approximately 100 additional queries loaded quotes.

\- Approximately 101 database queries were executed per request.



Baseline Performance



\- p50: 114.75 ms

\- p99:155.99 ms

\- HTTP failures: 0%



Before Execution Plan



text

QUERY PLAN

\--SCAN q

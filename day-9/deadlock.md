Day 9 - Reproduce and Resolve a Deadlock



Objective



Reproduce a classic two-resource deadlock across two SQL Server sessions, capture the deadlock victim message, and resolve it using consistent lock ordering.



Test Database



ThinkSchoolDay8



Test Table



dbo.BankAccounts



Initial data:



| AccountId | AccountName | Balance |

|---|---|---:|

| 1 | Alice | 1000 |

| 2 | Bob | 1000 |



Deadlock Reproduction



Session 1



sql

BEGIN TRANSACTION;



UPDATE dbo.BankAccounts

SET Balance = Balance - 100

WHERE AccountId = 1;



UPDATE dbo.BankAccounts

SET Balance = Balance + 100

WHERE AccountId = 2;



Session 1 first locks Account 1 and then attempts to acquire the lock on Account 2.



Session 2



sql

BEGIN TRANSACTION;



UPDATE dbo.BankAccounts

SET Balance = Balance - 50

WHERE AccountId = 2;



UPDATE dbo.BankAccounts

SET Balance = Balance + 50

WHERE AccountId = 1;



Session 2 first locks Account 2 and then attempts to acquire the lock on Account 1.



Deadlock Cycle



Session 1 holds Account 1 -> waits for Account 2.



Session 2 holds Account 2 -> waits for Account 1.



This creates a circular wait condition.



Deadlock Victim Message



SQL Server detected the deadlock and selected one transaction as the victim.



Observed error:



Msg 1205, Level 13, State 51



Transaction (Process ID 20) was deadlocked on lock resources with another process and has been chosen as the deadlock victim.



Fix - Consistent Lock Ordering



Both transactions acquire Account 1 first and Account 2 second.



Session 1 - Fixed



sql

BEGIN TRANSACTION;



UPDATE dbo.BankAccounts

SET Balance = Balance - 100

WHERE AccountId = 1;



UPDATE dbo.BankAccounts

SET Balance = Balance + 100

WHERE AccountId = 2;



COMMIT;



**Session 2 - Fixed**



**sql**



**BEGIN TRANSACTION;**



**UPDATE dbo.BankAccounts**

**SET Balance = Balance - 50**

**WHERE AccountId = 1;**



**UPDATE dbo.BankAccounts**

**SET Balance = Balance + 50**

**WHERE AccountId = 2;**



**COMMIT;**





**Session 2 - Fixed**



**sql**



**BEGIN TRANSACTION;**



**UPDATE dbo.BankAccounts**

**SET Balance = Balance - 50**

**WHERE AccountId = 1;**



**UPDATE dbo.BankAccounts**

**SET Balance = Balance + 50**

**WHERE AccountId = 2;**



**COMMIT;**





**Why the Fix Works**



**Both transactions acquire locks in the same order, so they cannot form the circular wait required for a deadlock.**





**Key Learning**



**Deadlocks occur when transactions acquire resources in conflicting orders. Consistent lock ordering prevents the circular wait condition and reduces the risk of deadlocks.**
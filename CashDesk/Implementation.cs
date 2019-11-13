using CashDesk.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CashDesk
{
    /// <inheritdoc />
    public class DataAccess : IDataAccess
    {
        private CashDeskDataContext context;
        /// <inheritdoc />
        public Task InitializeDatabaseAsync() {
            if(context != null)
            {
                throw new InvalidOperationException("Database already exists");
            }
            
            
            context = new CashDeskDataContext();
            return Task.CompletedTask;
        }

        public void CheckIfDataBaseExists()
        {
            if (context == null)
            {
                throw new InvalidOperationException("Database does not exist yet, you have to initialize it!");
            }
        }

        /// <inheritdoc />
        public async Task<int> AddMemberAsync(string firstName, string lastName, DateTime birthday)
        {
            CheckIfDataBaseExists();
            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            {
                throw new ArgumentException("FirstName or Lastname is null");
            }
            //Wurde von der Lösung kopiert
            if (await context.Members.AnyAsync(m => m.LastName == lastName))
            {
                throw new DuplicateNameException();
            }

            Member m = new Member
            {
                FirstName = firstName,
                LastName = lastName,
                Birthday = birthday,
            };
            context.Members.Add(m);
            await context.SaveChangesAsync();
            return m.MemberNumber;

        }

        public async Task DeleteMemberAsync(int memberNumber)
        {
            CheckIfDataBaseExists();

            Member member;
            try
            {
                member = await context.Members.FirstAsync(m => m.MemberNumber == memberNumber);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException();
            }

            context.Members.Remove(member);

            await context.SaveChangesAsync();
        }



        /// <inheritdoc />
        public async Task<IMembership> JoinMemberAsync(int memberNumber)
        {
            CheckIfDataBaseExists();

            if (await context.Memberships.AnyAsync(m => m.Member.MemberNumber == memberNumber
                    && DateTime.Now >= m.Begin && DateTime.Now <= m.End))
            {
                throw new AlreadyMemberException();
            }

            var newMembership = new Membership
            {
                Member = await context.Members.FirstAsync(m => m.MemberNumber == memberNumber),
                Begin = DateTime.Now,
                End = DateTime.MaxValue
            };
            context.Memberships.Add(newMembership);
            await context.SaveChangesAsync();

            return newMembership;
        }

        /// <inheritdoc />
        public async Task<IMembership> CancelMembershipAsync(int memberNumber)
        {
            CheckIfDataBaseExists();

            Membership membership;
            try
            {
                membership = await context.Memberships.FirstAsync(m => m.Member.MemberNumber == memberNumber
                    && m.End == DateTime.MaxValue);
            }
            catch (InvalidOperationException)
            {
                throw new NoMemberException();
            }

            membership.End = DateTime.Now;

            await context.SaveChangesAsync();

            return membership;
        }

        /// <inheritdoc />
        public async Task DepositAsync(int memberNumber, decimal amount)
        {
            CheckIfDataBaseExists();

            Member member;
            try
            {
                member = await context.Members.FirstAsync(m => m.MemberNumber == memberNumber);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException();
            }

            Membership membership;
            try
            {
                membership = await context.Memberships.FirstAsync(m => m.Member.MemberNumber == memberNumber
                    && DateTime.Now >= m.Begin && DateTime.Now <= m.End);
            }
            catch (InvalidOperationException)
            {
                throw new NoMemberException();
            }

            var newDeposit = new Deposit
            {
                Membership = membership,
                Amount = amount
            };
            context.Deposits.Add(newDeposit);
            await context.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IDepositStatistics>> GetDepositStatisticsAsync()
        {
            CheckIfDataBaseExists();

            return (await context.Deposits.Include("Membership.Member").ToArrayAsync())
                .GroupBy(d => new { d.Membership.Begin.Year, d.Membership.Member })
                .Select(i => new DepositStatistics
                {
                    Year = i.Key.Year,
                    Member = i.Key.Member,
                    TotalAmount = i.Sum(d => d.Amount)
                });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (context != null)
            {
                context.Dispose();
                context = null;
            }
        }
    }
}

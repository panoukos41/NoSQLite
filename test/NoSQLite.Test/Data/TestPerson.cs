using Bogus;

namespace NoSQLite.Test.Data;

public sealed record TestPerson
{
    public string Email { get; set; }

    public string Name { get; set; }

    public string Surname { get; set; }

    public string Phone { get; set; }

    public DateOnly Birthdate { get; set; }
}

public sealed class PersonFaker : Faker<TestPerson>
{
    public PersonFaker()
    {
        RuleFor(x => x.Email, f => f.Person.Email);
        RuleFor(x => x.Name, f => f.Person.FirstName);
        RuleFor(x => x.Surname, f => f.Person.LastName);
        RuleFor(x => x.Phone, f => f.Person.Phone);
        RuleFor(x => x.Birthdate, f => f.Date.PastDateOnly(20));
    }
}
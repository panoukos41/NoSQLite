using Bogus;

namespace NoSQLite.Test.Data;

public sealed record TestPerson
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateOnly Birthdate { get; set; }
}

public sealed class PersonFaker : Faker<TestPerson>
{
    public PersonFaker()
    {
        RuleFor(x => x.Id, f => f.IndexFaker);
        RuleFor(x => x.Email, f => f.Person.Email);
        RuleFor(x => x.Name, f => f.Person.FirstName);
        RuleFor(x => x.Surname, f => f.Person.LastName);
        RuleFor(x => x.Phone, f => f.Person.Phone);
        RuleFor(x => x.Birthdate, f => f.Date.PastDateOnly(20));
    }
}
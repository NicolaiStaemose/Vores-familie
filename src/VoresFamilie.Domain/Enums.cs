namespace VoresFamilie.Domain;

public enum Role { Admin, Voksen, Barn }

public enum Area { Calendars, Homework, MealPlan, Shopping, Routines, Cleaning, Todo }

public enum AccessLevel { None = 0, View = 1, Edit = 2 }

public class DomainException(string message) : Exception(message);

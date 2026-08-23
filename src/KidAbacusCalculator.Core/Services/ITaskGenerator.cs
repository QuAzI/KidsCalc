using KidAbacusCalculator.Core.Models;

namespace KidAbacusCalculator.Core.Services;

public interface ITaskGenerator
{
    TaskItem Create(int maximumAnswer);
}

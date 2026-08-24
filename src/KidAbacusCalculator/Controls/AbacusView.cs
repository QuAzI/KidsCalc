using KidAbacusCalculator.Core.Services;
using KidAbacusCalculator.Drawings;

namespace KidAbacusCalculator.Controls;

public sealed class AbacusView : GraphicsView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(int),
        typeof(AbacusView),
        0,
        BindingMode.OneWay,
        propertyChanged: OnValueChanged);

    public static readonly BindableProperty MaximumTaskPlaceValueProperty = BindableProperty.Create(
        nameof(MaximumTaskPlaceValue),
        typeof(int),
        typeof(AbacusView),
        1,
        BindingMode.OneWay,
        propertyChanged: OnMaximumTaskPlaceValueChanged);

    private readonly AbacusDrawable _abacusDrawable;

    public AbacusView()
    {
        _abacusDrawable = new AbacusDrawable(new AbacusBuilder());
        Drawable = _abacusDrawable;
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int MaximumTaskPlaceValue
    {
        get => (int)GetValue(MaximumTaskPlaceValueProperty);
        set => SetValue(MaximumTaskPlaceValueProperty, value);
    }

    private static void OnValueChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        var view = (AbacusView)bindable;
        var previous = (int)oldValue;
        var target = (int)newValue;

        // Начальный binding срабатывает до создания нативного WinUI handler.
        // Запуск анимации в этот момент приводит к падению GraphicsView с E_INVALIDARG.
        if (view.Handler is null)
        {
            view._abacusDrawable.SetTransition(target, target, 1d);
            return;
        }

        view.AbortAnimation("AbacusValue");

        // Короткая анимация перемещает только бусины и сразу перерисовывает
        // GraphicsView, поэтому быстрые нажатия не блокируют интерфейс.
        view.Animate(
            "AbacusValue",
            progress =>
            {
                view._abacusDrawable.SetTransition(previous, target, progress);
                view.Invalidate();
            },
            start: 0d,
            end: 1d,
            rate: 16u,
            length: 220u,
            easing: Easing.CubicOut,
            finished: (_, _) =>
            {
                view._abacusDrawable.SetTransition(target, target, 1d);
                view.Invalidate();
            });
    }

    private static void OnMaximumTaskPlaceValueChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        var view = (AbacusView)bindable;

        // Серый разряд остаётся частью тех же счётов: меняется только палитра,
        // а обработка кнопок и анимация значения продолжают работать как раньше.
        view._abacusDrawable.SetMaximumTaskPlaceValue((int)newValue);
        view.Invalidate();
    }
}

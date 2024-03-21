using System;

namespace Python.Runtime;

partial class PyFloat : IComparable<double>, IComparable<float>
    , IEquatable<double>, IEquatable<float>
    , IComparable<PyFloat?>, IEquatable<PyFloat?>
{
    public override bool Equals(object o)
    {
        using var _ = Py.GIL();
        return o switch
        {
            double f64 => this.Equals(f64),
            float f32 => this.Equals(f32),
            _ => base.Equals(o),
        };
    }

    public int CompareTo(double other) => this.ToDouble().CompareTo(other);

    public int CompareTo(float other) => this.ToDouble().CompareTo(other);

    public bool Equals(double other) => this.ToDouble().Equals(other);

    public bool Equals(float other) => this.ToDouble().Equals(other);

    public int CompareTo(PyFloat? other)
    {
        return other is null ? 1 : this.CompareTo(other.BorrowNullable());
    }

    public bool Equals(PyFloat? other) => base.Equals(other);
}

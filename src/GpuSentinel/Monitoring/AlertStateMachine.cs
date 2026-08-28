namespace GpuSentinel.Monitoring;

public sealed class AlertStateMachine
{
    private AlertAssessment _current = new(AlertLevel.Normal, "Starting monitor…");
    private AlertAssessment? _candidate;
    private int _candidateCount;

    public AlertAssessment Current => _current;

    public bool Push(AlertAssessment assessment)
    {
        if (assessment.Level == AlertLevel.Offline)
            return SetCurrent(assessment);

        if (assessment.Level == _current.Level)
        {
            _current = assessment;
            _candidate = null;
            _candidateCount = 0;
            return false;
        }

        if (_candidate?.Level != assessment.Level)
        {
            _candidate = assessment;
            _candidateCount = 1;
        }
        else
        {
            _candidate = assessment;
            _candidateCount++;
        }

        var isEscalating = assessment.Level > _current.Level;
        var requiredSamples = isEscalating
            ? assessment.Level == AlertLevel.Critical ? 2 : 3
            : 5;

        return _candidateCount >= requiredSamples && SetCurrent(assessment);
    }

    private bool SetCurrent(AlertAssessment assessment)
    {
        var changed = assessment.Level != _current.Level;
        _current = assessment;
        _candidate = null;
        _candidateCount = 0;
        return changed;
    }
}

using System.Collections.Generic;

namespace VectorEditor.Services
{
    public class UndoService
    {
        // نحتفظ داخليًا بسعة = عدد خطوات الرجوع + 1 (الحالة الحالية)
        private readonly int _capacity;
        private readonly List<string> _timeline = new();
        private int _index = -1;

        /// <param name="maxSteps">عدد خطوات الرجوع التي تريدها (مثلاً 5)</param>
        public UndoService(int maxSteps = 5)
        {
            // نخزن maxSteps + 1 حالات لضمان 5 رجوعات كاملة من الحالة الحالية
            int steps = maxSteps < 1 ? 1 : maxSteps;
            _capacity = steps + 1;
        }

        /// <summary>حفظ حالة جديدة (نمسح أي Redo لاحق)، لا نسجل تكرار الحالة الحالية.</summary>
        public void Save(string state, bool force = false)
        {
            if (!force && _index >= 0 && _timeline[_index] == state)
                return;

            if (_index < _timeline.Count - 1)
                _timeline.RemoveRange(_index + 1, _timeline.Count - (_index + 1));

            _timeline.Add(state);
            _index = _timeline.Count - 1;

            while (_timeline.Count > _capacity)
            {
                _timeline.RemoveAt(0);
                _index--;
            }
        }

        /// <summary>تراجع لخطوة سابقة (أو null إن لم يوجد).</summary>
        public string Undo()
        {
            if (_index > 0)
            {
                _index--;
                return _timeline[_index];
            }
            return null;
        }

        /// <summary>ابدأ خطًا زمنيًا جديدًا من حالة واحدة (مثلاً بعد التحميل).</summary>
        public void ResetWith(string initialState)
        {
            _timeline.Clear();
            _timeline.Add(initialState);
            _index = 0;
        }
    }
}

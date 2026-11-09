using System;

namespace ProjectW.MilestonePrototype
{
    public sealed class NotificationEntry
    {
        public string Category;
        public string Title;
        public string Message;
        public int Day;
        public float ExpiresAt;
    }

    public sealed class NotificationCenter
    {
        private readonly NotificationEntry[] history;
        private readonly float bannerDurationSeconds;
        private int count;

        public NotificationCenter(int historyCapacity, float bannerDurationSeconds)
        {
            if (historyCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(historyCapacity));
            if (bannerDurationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(bannerDurationSeconds));

            history = new NotificationEntry[historyCapacity];
            this.bannerDurationSeconds = bannerDurationSeconds;
        }

        public int Count => count;

        public NotificationEntry Get(int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return history[index];
        }

        public void Add(string category, string title, string message, int day, float now)
        {
            int last = Math.Min(count, history.Length - 1);
            for (int index = last; index > 0; index--)
                history[index] = history[index - 1];

            history[0] = new NotificationEntry
            {
                Category = category ?? string.Empty,
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                Day = day,
                ExpiresAt = now + bannerDurationSeconds
            };
            if (count < history.Length) count++;
        }

        public int ActiveBannerCount(float now, int maximum)
        {
            int active = 0;
            for (int index = 0; index < count && active < maximum; index++)
                if (history[index].ExpiresAt > now) active++;
            return active;
        }

        public NotificationEntry ActiveBannerAt(float now, int activeIndex)
        {
            int found = 0;
            for (int index = 0; index < count; index++)
            {
                if (history[index].ExpiresAt <= now) continue;
                if (found == activeIndex) return history[index];
                found++;
            }

            throw new ArgumentOutOfRangeException(nameof(activeIndex));
        }

        public float NextExpiry(float now)
        {
            float next = float.MaxValue;
            for (int index = 0; index < count; index++)
                if (history[index].ExpiresAt > now && history[index].ExpiresAt < next)
                    next = history[index].ExpiresAt;
            return next;
        }

        public void Clear()
        {
            Array.Clear(history, 0, history.Length);
            count = 0;
        }
    }
}

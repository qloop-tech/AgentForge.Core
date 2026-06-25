using AgentForge.Verticals.Abstractions;

namespace AgentForge.Verticals.Travel;

public sealed class TravelScheduledActionHandler : IScheduledActionHandler
{
    public Task<IReadOnlyList<ScheduledMessage>> HandleAsync(ScheduledAction action, CancellationToken ct = default)
    {
        IReadOnlyList<ScheduledMessage> messages = action.ActionType switch
        {
            TravelScheduledActionTypes.PreDeparture7Day =>
            [
                new ScheduledMessage(
                    action.ChatId,
                    $$"""
                    ⏰ *7 Days to Go!*

                    Your *{{action.ItemName}}* trip is just 7 days away! 🎉

                    ✅ *Checklist:*
                    • Check your ID/Passport validity
                    • Pack weather-appropriate clothes
                    • Download offline maps
                    • Charge all devices
                    • Inform your bank about travel
                    """)
            ],

            TravelScheduledActionTypes.PreDeparture1Day =>
            [
                new ScheduledMessage(
                    action.ChatId,
                    $$"""
                    🚀 *Tomorrow is the day!*

                    *{{action.ItemName}}* — Departure tomorrow!

                    📌 Your driver will arrive at *6:00 AM*
                    📞 Driver contact: +91 98765 43210
                    🎒 Keep your documents handy
                    Have an amazing trip! ✈️
                    """)
            ],

            TravelScheduledActionTypes.DepartureDay =>
            [
                new ScheduledMessage(
                    action.ChatId,
                    $$"""
                    🎉 *Today's the Day! Your {{action.ItemName}} adventure begins!*

                    Safe travels and make wonderful memories! 📸
                    """)
            ],

            TravelScheduledActionTypes.PostTripFeedback =>
            [
                new ScheduledMessage(
                    action.ChatId,
                    $$"""
                    🎉 *Welcome back from your {{action.ItemName}} adventure!*

                    We hope you had an amazing time! Your feedback helps us improve. 🙏
                    """),
                new ScheduledMessage(
                    action.ChatId,
                    """
                    ⭐ *We'd love your feedback!*

                    Please rate your overall experience by replying with a number:

                    1️⃣  — Poor
                    2️⃣  — Below Average
                    3️⃣  — Average
                    4️⃣  — Good
                    5️⃣  — Excellent

                    Just reply with *1*, *2*, *3*, *4*, or *5* 👆
                    """)
            ],

            _ => throw new InvalidOperationException($"Unsupported travel scheduled action type '{action.ActionType}'.")
        };

        return Task.FromResult(messages);
    }
}

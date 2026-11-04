using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;

namespace AirwayHVAC.SmsAi.Application.Services;

public sealed class ConversationFlowEngine
{
    public ConversationFlowResult ProcessCustomerMessage(Conversation conversation, string customerMessage)
        => ProcessCustomerMessage(conversation, customerMessage, ConversationChannel.Voice);

    public ConversationFlowResult ProcessCustomerMessage(Conversation conversation, string customerMessage, ConversationChannel channel)
    {
        var trimmed = customerMessage.Trim();

        CaptureFields(conversation, trimmed);
        AdvanceStage(conversation, trimmed);

        var reply = BuildReply(conversation, channel);
        return new ConversationFlowResult(reply, conversation.Stage);
    }

    private static void CaptureFields(Conversation conversation, string text)
    {
        if (string.IsNullOrWhiteSpace(conversation.Issue) && LooksLikeIssue(text))
        {
            conversation.SetIssue(text);
        }

        if (string.IsNullOrWhiteSpace(conversation.Address) && LooksLikeAddress(text))
        {
            conversation.SetAddress(text);
        }

        if (string.IsNullOrWhiteSpace(conversation.Urgency))
        {
            var urgency = ParseUrgency(text);
            if (urgency is not null)
            {
                conversation.SetUrgency(urgency);
            }
        }

        if (string.IsNullOrWhiteSpace(conversation.PreferredTime) && LooksLikeAppointment(text))
        {
            conversation.SetPreferredTime(text);
        }
    }

    private static void AdvanceStage(Conversation conversation, string customerText)
    {
        if (!string.IsNullOrWhiteSpace(conversation.Issue)
            && !string.IsNullOrWhiteSpace(conversation.Address)
            && !string.IsNullOrWhiteSpace(conversation.Urgency)
            && !string.IsNullOrWhiteSpace(conversation.PreferredTime))
        {
            conversation.SetStage(ConversationStage.Confirm);
        }

        if (conversation.Stage == ConversationStage.Confirm)
        {
            var confirmation = ParseConfirmation(customerText);
            if (confirmation.HasValue)
            {
                conversation.SetConfirmation(confirmation.Value);
                if (confirmation.Value)
                {
                    conversation.SetStage(ConversationStage.Completed);
                }
                else
                {
                    conversation.SetStage(ConversationStage.Appointment);
                    conversation.ClearPreferredTime();
                }

                return;
            }
        }

        if (string.IsNullOrWhiteSpace(conversation.Issue))
        {
            conversation.SetStage(ConversationStage.Issue);
        }
        else if (string.IsNullOrWhiteSpace(conversation.Address))
        {
            conversation.SetStage(ConversationStage.Address);
        }
        else if (string.IsNullOrWhiteSpace(conversation.Urgency))
        {
            conversation.SetStage(ConversationStage.Urgency);
        }
        else if (string.IsNullOrWhiteSpace(conversation.PreferredTime))
        {
            conversation.SetStage(ConversationStage.Appointment);
        }
        else if (conversation.Stage != ConversationStage.Completed)
        {
            conversation.SetStage(ConversationStage.Confirm);
        }
    }

    private static string BuildReply(Conversation conversation, ConversationChannel channel)
        => BuildVoiceReply(conversation);

    private static string BuildVoiceReply(Conversation conversation)
        => conversation.Stage switch
        {
            ConversationStage.Issue => "Please tell me what issue you are having with your heating or cooling system.",
            ConversationStage.Address => "Thanks. What is the service address?",
            ConversationStage.Urgency => "Is this an emergency, same-day, or flexible?",
            ConversationStage.Appointment => "What day and time works best for a technician visit?",
            ConversationStage.Confirm => $"Let me confirm. You need help with {conversation.Issue}, at {conversation.Address}. The urgency is {conversation.Urgency}, and your preferred time is {conversation.PreferredTime}. Is that correct?",
            ConversationStage.Completed => "Great. Your service request is confirmed. Our team will contact you shortly to finalize the appointment.",
            _ => "Could you share more details so I can help schedule your HVAC service?"
        };

    private static bool LooksLikeIssue(string text)
        => text.Contains("ac", StringComparison.OrdinalIgnoreCase)
           || text.Contains("heat", StringComparison.OrdinalIgnoreCase)
           || text.Contains("furnace", StringComparison.OrdinalIgnoreCase)
           || text.Contains("cool", StringComparison.OrdinalIgnoreCase)
           || text.Contains("hvac", StringComparison.OrdinalIgnoreCase)
           || text.Contains("not working", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAddress(string text)
        => text.Any(char.IsDigit)
           && (text.Contains("st", StringComparison.OrdinalIgnoreCase)
               || text.Contains("street", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ave", StringComparison.OrdinalIgnoreCase)
               || text.Contains("avenue", StringComparison.OrdinalIgnoreCase)
               || text.Contains("rd", StringComparison.OrdinalIgnoreCase)
               || text.Contains("road", StringComparison.OrdinalIgnoreCase)
               || text.Contains("dr", StringComparison.OrdinalIgnoreCase)
               || text.Contains("drive", StringComparison.OrdinalIgnoreCase)
               || text.Contains("blvd", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeAppointment(string text)
        => text.Contains("am", StringComparison.OrdinalIgnoreCase)
           || text.Contains("pm", StringComparison.OrdinalIgnoreCase)
           || text.Contains("today", StringComparison.OrdinalIgnoreCase)
           || text.Contains("tomorrow", StringComparison.OrdinalIgnoreCase)
           || text.Contains("morning", StringComparison.OrdinalIgnoreCase)
           || text.Contains("afternoon", StringComparison.OrdinalIgnoreCase)
           || text.Contains("evening", StringComparison.OrdinalIgnoreCase);

    private static string? ParseUrgency(string text)
    {
        if (text.Contains("emergency", StringComparison.OrdinalIgnoreCase) || text.Contains("urgent", StringComparison.OrdinalIgnoreCase) || text.Contains("asap", StringComparison.OrdinalIgnoreCase))
        {
            return "Emergency";
        }

        if (text.Contains("same day", StringComparison.OrdinalIgnoreCase) || text.Contains("today", StringComparison.OrdinalIgnoreCase))
        {
            return "Same-day";
        }

        if (text.Contains("flexible", StringComparison.OrdinalIgnoreCase) || text.Contains("anytime", StringComparison.OrdinalIgnoreCase))
        {
            return "Flexible";
        }

        return null;
    }

    private static bool? ParseConfirmation(string text)
    {
        // Normalize first: drop punctuation so "Yes." / "Yes!" match, and
        // "that's correct" becomes "thats correct". Check the NO branch
        // BEFORE the YES branch, otherwise "not correct" would match the
        // YES branch via "correct".
        var normalized = new string(text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray())
            .Trim();
        var lowered = normalized.ToLowerInvariant();

        if (lowered.Equals("no", StringComparison.Ordinal)
            || lowered.Equals("n", StringComparison.Ordinal)
            || lowered.Contains("not correct", StringComparison.Ordinal)
            || lowered.Contains("incorrect", StringComparison.Ordinal)
            || lowered.Contains("not right", StringComparison.Ordinal)
            || lowered.Contains("change", StringComparison.Ordinal)
            || lowered.Contains("wrong", StringComparison.Ordinal))
        {
            return false;
        }

        if (lowered.Equals("yes", StringComparison.Ordinal)
            || lowered.Equals("y", StringComparison.Ordinal)
            || lowered.Equals("yeah", StringComparison.Ordinal)
            || lowered.Equals("yep", StringComparison.Ordinal)
            || lowered.Equals("sure", StringComparison.Ordinal)
            || lowered.Equals("ok", StringComparison.Ordinal)
            || lowered.Equals("okay", StringComparison.Ordinal)
            || lowered.Equals("correct", StringComparison.Ordinal)
            || lowered.Equals("right", StringComparison.Ordinal)
            || lowered.Contains("confirm", StringComparison.Ordinal)
            || lowered.Contains("correct", StringComparison.Ordinal)
            || lowered.Contains("thats right", StringComparison.Ordinal)
            || lowered.Contains("thats correct", StringComparison.Ordinal)
            || lowered.Contains("sounds good", StringComparison.Ordinal)
            || lowered.Contains("looks good", StringComparison.Ordinal)
            || lowered.Contains("all good", StringComparison.Ordinal))
        {
            return true;
        }

        return null;
    }
}

public sealed record ConversationFlowResult(string ReplyMessage, ConversationStage Stage);

/** Grouped sample questions, mirrored from the BasicEmail demo's "Try asking" panel. */
export interface SuggestionGroup {
  heading: string;
  questions: string[];
}

export const SUGGESTIONS: SuggestionGroup[] = [
  {
    heading: 'Orders & Shipping',
    questions: [
      'How long does standard shipping take?',
      'Is shipping free on my order?',
      'How do I track my order?',
      'Can I cancel or change my order?',
      'Do you offer same-day delivery?',
    ],
  },
  {
    heading: 'Returns & Payments',
    questions: [
      'What is your return policy?',
      'How long does a refund take?',
      'My item arrived damaged — what do I do?',
      'What payment methods do you accept?',
      'How do I apply a discount code?',
    ],
  },
  {
    heading: 'Account & Rewards',
    questions: [
      'How do I reset my password?',
      'How do I earn and redeem rewards points?',
      'How do I buy or redeem a gift card?',
      'Do you ship internationally?',
      'How do I delete my account?',
    ],
  },
  {
    heading: 'Test Escalation',
    questions: [
      "I placed an order 2 weeks ago and it still hasn't arrived.",
      'I was charged twice for the same order.',
      'I need to speak to a human agent.',
      'My account was hacked — please help.',
    ],
  },
];

import torch
import torch.nn as nn


class ActorCritic(nn.Module):
    def __init__(self, observation_size, action_size):
        super().__init__()

        self.shared = nn.Sequential(
            nn.Linear(observation_size, 128),
            nn.ReLU(),
            nn.Linear(128, 128),
            nn.ReLU()
        )

        self.actor = nn.Linear(128, action_size)
        self.critic = nn.Linear(128, 1)

    def forward(self, x):
        features = self.shared(x)
        logits = self.actor(features)
        value = self.critic(features)

        return logits, value


def load_ppo_model(model_path, observation_size, action_size):
    model = ActorCritic(
        observation_size=observation_size,
        action_size=action_size
    )

    state_dict = torch.load(
        model_path,
        map_location="cpu"
    )

    model.load_state_dict(state_dict)
    model.eval()

    return model


def select_greedy_action(model, observation):
    obs_tensor = torch.tensor(
        observation,
        dtype=torch.float32
    ).unsqueeze(0)

    with torch.no_grad():
        logits, _ = model(obs_tensor)
        action = torch.argmax(logits, dim=-1).item()

    return action
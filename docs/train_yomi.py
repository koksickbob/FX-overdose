import torch
from unsloth import FastLanguageModel
from datasets import load_dataset
from trl import SFTTrainer
from transformers import TrainingArguments
from unsloth.chat_templates import get_chat_template
import os

# 1. Configuration
max_seq_length = 1024 # 모바일(Android) 환경에 맞춘 최소 컨텍스트 윈도우 (여유 있게 1024로 늘림)
dtype = None # 자동 감지 (RTX 4060의 경우 bf16/fp16 지원)
load_in_4bit = True # 4bit 양자화로 VRAM 최소화 (RTX 4060 8GB에서 안정적으로 돌아가게 세팅)

print("🚀 Unsloth 모델 로드 시작: Qwen/Qwen2.5-3B-Instruct")
# 2. 베이스 모델 로드 (모바일 및 저사양 VRAM 환경을 위해 4bit 필수)
model, tokenizer = FastLanguageModel.from_pretrained(
    model_name = "Qwen/Qwen2.5-3B-Instruct",
    max_seq_length = max_seq_length,
    dtype = dtype,
    load_in_4bit = load_in_4bit,
    token = None,
)

# 3. LoRA 어댑터 설정 (학습시킬 파라미터 지정)
model = FastLanguageModel.get_peft_model(
    model,
    r = 16,
    target_modules = ["q_proj", "k_proj", "v_proj", "o_proj",
                      "gate_proj", "up_proj", "down_proj"],
    lora_alpha = 32,
    lora_dropout = 0,
    bias = "none",
    use_gradient_checkpointing = "unsloth",
    random_state = 3407,
    use_rslora = False,
    loftq_config = None,
)

# 4. 데이터셋 로드 및 ChatML 포맷팅
tokenizer = get_chat_template(
    tokenizer,
    chat_template = "chatml",
    mapping = {"role" : "role", "content" : "content", "user" : "user", "assistant" : "assistant"},
)

def formatting_prompts_func(examples):
    convos = examples["messages"]
    texts = [tokenizer.apply_chat_template(convo, tokenize = False, add_generation_prompt = False) for convo in convos]
    return { "text" : texts, }

print(f"📖 데이터셋 로드 중: {os.path.abspath('yomi_dataset.jsonl')}")
dataset = load_dataset("json", data_files={"train": "yomi_dataset.jsonl"}, split="train")
dataset = dataset.map(formatting_prompts_func, batched = True)

# 학습(Train) / 검증(Validation) 데이터셋 분리 (95:5 비율)
dataset = dataset.train_test_split(test_size=0.05, seed=3407)
train_dataset = dataset["train"]
eval_dataset = dataset["test"]
print(f"📊 학습 데이터: {len(train_dataset)}개, 검증 데이터: {len(eval_dataset)}개")

# 5. SFT 트레이너 설정
trainer = SFTTrainer(
    model = model,
    tokenizer = tokenizer,
    train_dataset = train_dataset,
    eval_dataset = eval_dataset,
    dataset_text_field = "text",
    max_seq_length = max_seq_length,
    dataset_num_proc = 2,
    packing = False,
    args = TrainingArguments(
        per_device_train_batch_size = 2,
        gradient_accumulation_steps = 4,
        warmup_steps = 10,
        num_train_epochs = 2, # 5000개 데이터를 2번 완주 (max_steps 대신 사용)
        learning_rate = 2e-4,
        fp16 = not torch.cuda.is_bf16_supported(),
        bf16 = torch.cuda.is_bf16_supported(),
        logging_steps = 10,
        eval_strategy = "steps",
        eval_steps = 50, # 50 스텝마다 검증 데이터로 과적합(Overfitting) 체크
        optim = "adamw_8bit",
        weight_decay = 0.01,
        lr_scheduler_type = "linear",
        seed = 3407,
        output_dir = "outputs",
        save_strategy = "no", # SFTConfig Pickling 버그 우회
    ),
)

# 6. 파인튜닝 학습 시작
print("🔥 요미(Yomi) 3B 페르소나 파인튜닝 학습을 시작합니다! (RTX 4060 8GB 기준 약 1~2시간 소요 예정)")
trainer_stats = trainer.train()

# 7. GGUF (Q4_K_M) 파일 추출 (유니티 모바일 용)
print("📦 학습 완료! 유니티 모바일 환경용 GGUF(Q4_K_M) 파일 변환을 즉시 시작합니다...")
# GGUF 변환 (라마.cpp 툴체인을 백그라운드에서 사용)
model.save_pretrained_gguf("yomi_3b_q4_k_m", tokenizer, quantization_method = "q4_k_m")

print("✅ 파인튜닝 및 GGUF 변환이 완벽하게 완료되었습니다!")
print(f"✅ 생성된 GGUF 폴더/파일 경로: {os.path.abspath('yomi_3b_q4_k_m')}")

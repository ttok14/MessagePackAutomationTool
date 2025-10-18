메시지팩 바이너리 데이터 자동화 프로그램 

1. 디렉터리 하나에 모든 필요한 csv 데이터들 준비 (데이터 테이블 및 Enum 테이블)
    (e.g ItemTable)
    | ID | ItemType | NameKey | Price |
    |----|----------|---------|-------|
    | 1  | Default  | Sword   | 1000  |
    | 2  | Default  | Wand    | 1500  |
    | 3  | Default  | Shield  | 2000  |
              ↓ (to csv)
    ID,ItemType,NameKey,Price  
    1,Default,Sword,1000  
    2,Default,Wand,1500  
    3,Default,Shield,2000
    
    (e.g ItemTable_Schema)
    | Key | Type |
    |-----|----------------|
    | ID | uint |
    | ItemType | E_ItemType |
    | NameKey | string |
    | Price | uint |
              ↓ (to csv)
              
    Key,Type  
    ID,uint  
    ItemType,E_ItemType   
    NameKey,string  
    Price,uint
    
    (e.g EnumTable)      
    | EnumName | IsFlags | MemberName | Value | Description |
    |:---|:---|:---|:---|:---|
    | E_ItemType | False | None | 0 | 없음 |
    | E_ItemType | False | Default | 1 | 일반 아이템 |
    | E_ItemType | False | Weapon | 2 | 무기 아이템 |
    | E_CharacterType | False | None | 0 | 없음 |
    | E_CharacterType | False | Warrior | 1 | 강력한 전사! |
    | E_CharacterType | False | Thief | 2 | 도둑놈의 쉐키 |
               ↓ (to csv)
    EnumName,IsFlags,MemberName,Value,Description  
    E_ItemType,False,None,0,없음  
    E_ItemType,False,Default,1,일반 아이템  
    E_ItemType,False,Weapon,2,무기 아이템  
    E_CharacterType,False,None,0,없음  
    E_CharacterType,False,Warrior,1,강력한 전사!  
    E_CharacterType,False,Thief,2,도둑놈의 쉐키  


    <img width="652" height="255" alt="image" src="https://github.com/user-attachments/assets/62f23036-c08b-4ace-b5ad-b3b830048774" />

3. 툴 이용
 
     <img width="1849" height="232" alt="image" src="https://github.com/user-attachments/assets/520e3ef7-2ce8-4837-a7bc-8e226da175db" />

     <결과 파일들>
     
     <img width="183" height="172" alt="image" src="https://github.com/user-attachments/assets/a965390a-a0c7-443b-ae68-e674f829c6fa" />

      (binaries 폴더, 실제 MessagePack 이 Deserialize 가능)
      
     <img width="627" height="40" alt="image" src="https://github.com/user-attachments/assets/f7696a7b-ead9-4cb9-8a8f-83add1261e22" />

    <필요한 파일들>
    1. GameDBContainer.cs : 실제 데이터 타입별 Deserialize 함수 위치 및 GameDBContainer 에 딕셔너리 형태로 데이터 저장
    2. GameDBEnum : EnumTable 로 부터 생성된 모든 Enum 타입 위치 
    3. GameDBResolver : 커스텀 클래스들을 Serialize/Deserialize 하기 위한 Resolver
    4. binaries 폴더에 생성된 .bytes 파일들

    ++ 
    1. MyCompositeResolver.cs 파일 (빌드 결과 파일이 아닌 별도 첨부)
    -- 나머지는 불필요한 아티팩트 --
    
5. Unity 에서 사용하기 

    1. 필요한 파일들 유니티로 가져가기 
    2. MyCompositeResolver 필요 (이름 바꿔도 무방)
    3. 데이터 복원 전에 다음 코드 실행 
      (직접 MyCompositeResolver 내에 Cache 로 MessagePack 이 Formatter 를 찾을때 불필요한 순회를 줄이기 때문에 퍼포먼스상 유리)
      ```csharp
        MyCompositeResolver.Instance.Register(
            GameDB.Resolvers.GameDBContainerResolver.Instance,
            StandardResolver.Instance
            );

        MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions.WithResolver(MyCompositeResolver.Instance);
      ```
    4. Reflection 으로 데이터 복원하기 
      ```csharp
        var container = new GameDBContainer();
        string binaryDir= ".bytes 파일들이 위치한 폴더";
        foreach (var filePath in Directory.GetFiles(binaryDir))
        {
            if (filePath.EndsWith(".bytes"))
            {
                fileList.Add(Path.GetFileName(filePath));
                var tableTypeName = Path.GetFileNameWithoutExtension(filePath);
                var fieldName = tableTypeName + "_data";

                byte[] bytes = File.ReadAllBytes(filePath);

                var field = typeof(GameDBContainer).GetField(fieldName);

                var deserializedMethod = Type.GetType($"GameDB.{tableTypeName}").GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
                var result = deserializedMethod.Invoke(null, new[] { bytes });
                field.SetValue(container, result);
            }
        }
      ```

6. 파일 버전 관리용 메타데이터 사용


         ```json
        {
          "Version": "251019_031207",
          "TotalHash": "bbc66182a1e45241b51de6f843e85e2301eb167dffcdcc97c4dfdc8d05f9e7ea",
          "Files": [
            {
              "Name": "CharacterTable",
              "Hash": "53610e8ae608cb77d62969ddef9661601af2d6fdff09a60d49bdc3d0fbfb84c5",
              "ByteSize": 129
            },
            {
              "Name": "ItemTable",
              "Hash": "24ef1da6c498d16c06093692d3cce39f40c275fc8352f8f66a64f9843a502ef9",
              "ByteSize": 154
            }
          ]
        }

   Version : {연월일_시간분초} 로 버저닝한 문자열  
   TotalHash : 모든 개별 테이블들의 해쉬를 재가공한 해쉬, 테이블 전체에 대한 버전 무결성 검증용  
   Files : 개별 테이블들  
       Name : 테이블명  
       Hash : 테이블 해쉬  
       ByteSize : 바이트 사이즈
